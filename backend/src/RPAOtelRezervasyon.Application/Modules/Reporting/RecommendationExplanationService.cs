using System.Globalization;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Application.Modules.Reporting;

public sealed class RecommendationExplanationService(
    IRecommendationExplanationProvider provider,
    RecommendationExplanationOptions options)
{
    private const decimal BalancePremiumLimit = 0.10m;

    public async Task<IReadOnlyList<RecommendationReportOption>> ExplainAsync(
        string venueName,
        IReadOnlyList<HotelReportRow> rankedHotels,
        IReadOnlyList<string> unresolvedHotels,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(venueName);
        ArgumentNullException.ThrowIfNull(rankedHotels);
        ArgumentNullException.ThrowIfNull(unresolvedHotels);
        if (rankedHotels.Count == 0)
        {
            throw new ArgumentException("En az bir sıralı otel olmalıdır.", nameof(rankedHotels));
        }

        var priceComparable = rankedHotels.Select(row => row.Price.Currency).Distinct(StringComparer.Ordinal).Count() == 1;
        var transport = BestTransport(rankedHotels);
        var cheapest = priceComparable
            ? rankedHotels.OrderBy(row => row.Price.Amount)
                .ThenBy(row => row.DistanceKind)
                .ThenBy(row => row.DistanceKind == DistanceKind.Road ? row.DurationSeconds : 0)
                .ThenBy(row => row.DistanceMeters)
                .ThenBy(row => row.Name, StringComparer.Ordinal)
                .First()
            : null;
        var balanced = priceComparable && cheapest is not null
            ? BestTransport(rankedHotels.Where(row => row.Price.Amount - cheapest.Price.Amount <= cheapest.Price.Amount * BalancePremiumLimit).ToArray())
            : null;

        var optionSeeds = new[]
        {
            Seed(RecommendationStrategy.BudgetPriority, cheapest, rankedHotels, cheapest, transport, balanced),
            Seed(RecommendationStrategy.TransportPriority, transport, rankedHotels, cheapest, transport, balanced),
            Seed(RecommendationStrategy.Balanced, balanced, rankedHotels, cheapest, transport, balanced),
        };

        IReadOnlyDictionary<RecommendationStrategy, IReadOnlyList<string>> reasonCodes = new Dictionary<RecommendationStrategy, IReadOnlyList<string>>();
        if (options.Enabled)
        {
            try
            {
                var request = new RecommendationExplanationRequest(
                    venueName,
                    rankedHotels.Select((hotel, index) => new RecommendationExplanationHotel(
                        index + 1,
                        hotel.Name,
                        hotel.Price.Amount,
                        hotel.Price.Currency,
                        hotel.DistanceMeters,
                        hotel.DurationSeconds,
                        hotel.DistanceKind,
                        hotel.Location.Latitude,
                        hotel.Location.Longitude,
                        hotel.WalkingRouteRequested,
                        hotel.WalkingMetrics?.DistanceMeters,
                        hotel.WalkingMetrics?.DurationSeconds)).ToArray(),
                    unresolvedHotels.ToArray(),
                    optionSeeds.Where(seed => seed.Hotel is not null)
                        .Select(seed => new RecommendationExplanationPolicyOption(seed.Strategy, seed.Hotel!.Name, seed.AllowedReasonCodes))
                        .ToArray());

                var outcome = await provider.GenerateAsync(request, cancellationToken).ConfigureAwait(false);
                if (IsValid(outcome, request))
                {
                    reasonCodes = outcome.Options.ToDictionary(option => option.Strategy, option => option.ReasonCodes);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // LLM is explanatory only; a failure must not block report creation.
            }
        }

        return optionSeeds.Select(seed => new RecommendationReportOption(
            seed.Strategy,
            seed.Hotel,
            seed.Hotel is { } hotel
                ? BuildExplanation(seed.Strategy, hotel, cheapest, transport, rankedHotels, seed.AllowedReasonCodes,
                    reasonCodes.GetValueOrDefault(seed.Strategy) ?? seed.AllowedReasonCodes)
                : seed.Explanation))
            .ToArray();
    }

    private static OptionSeed Seed(
        RecommendationStrategy strategy,
        HotelReportRow? selected,
        IReadOnlyList<HotelReportRow> rows,
        HotelReportRow? cheapest,
        HotelReportRow transport,
        HotelReportRow? balanced)
    {
        if (selected is null)
        {
            return new(strategy, null, [], "Farklı para birimlerindeki tutarlar ortak bir fiyat sıralaması veya denge seçeneği oluşturmak için karşılaştırılamaz.");
        }

        var codes = new List<string>
        {
            strategy switch
            {
                RecommendationStrategy.BudgetPriority => "lowest-price",
                RecommendationStrategy.TransportPriority => selected.DistanceKind == DistanceKind.Road ? "shortest-road-time" : "shortest-straightline-estimate",
                RecommendationStrategy.Balanced => "within-ten-percent-budget-band",
                _ => throw new ArgumentOutOfRangeException(nameof(strategy)),
            },
        };

        if (selected.WalkingMetrics is not null)
        {
            codes.Add("walking-route-available");
        }

        if (cheapest is not null && !ReferenceEquals(cheapest, transport))
        {
            var comparedAmount = selected.Name == cheapest.Name ? transport.Price.Amount : selected.Price.Amount;
            var gap = cheapest.Price.Amount == 0
                ? comparedAmount == 0 ? 0m : decimal.MaxValue
                : decimal.Abs(comparedAmount - cheapest.Price.Amount) / cheapest.Price.Amount;
            codes.Add(gap <= BalancePremiumLimit ? "small-price-gap" : "significant-price-gap");
        }

        var strategyCandidates = strategy switch
        {
            RecommendationStrategy.BudgetPriority => rows.Where(row => row.Price.Currency == selected.Price.Currency
                && row.Price.Amount == selected.Price.Amount).ToArray(),
            RecommendationStrategy.Balanced when cheapest is not null => rows.Where(row => row.Price.Currency == cheapest.Price.Currency
                && row.Price.Amount - cheapest.Price.Amount <= cheapest.Price.Amount * BalancePremiumLimit).ToArray(),
            _ => rows.ToArray(),
        };
        if (selected.DistanceKind == DistanceKind.Road
            && BestTransport(strategyCandidates).Name == selected.Name
            && strategyCandidates.Any(row => row.Name != selected.Name
                && row.DistanceKind == DistanceKind.Road
                && row.DurationSeconds == selected.DurationSeconds
                && row.DistanceMeters > selected.DistanceMeters))
        {
            codes.Add("shortest-road-distance-tiebreak");
        }

        return new(strategy, selected, codes, string.Empty);
    }

    private static bool IsValid(RecommendationExplanationOutcome outcome, RecommendationExplanationRequest request)
    {
        if (!outcome.IsSuccess || outcome.Options.Count != request.Options.Count
            || outcome.Options.Select(option => option.Strategy).Distinct().Count() != outcome.Options.Count)
        {
            return false;
        }

        foreach (var expected in request.Options)
        {
            var actual = outcome.Options.SingleOrDefault(option => option.Strategy == expected.Strategy);
            if (actual is null
                || actual.ReasonCodes.Count == 0
                || actual.ReasonCodes.Count != expected.AllowedReasonCodes.Count
                || actual.ReasonCodes.Distinct(StringComparer.Ordinal).Count() != actual.ReasonCodes.Count
                || actual.ReasonCodes.Any(code => !expected.AllowedReasonCodes.Contains(code, StringComparer.Ordinal)))
            {
                return false;
            }

        }

        return true;
    }

    private static string BuildExplanation(
        RecommendationStrategy strategy,
        HotelReportRow selected,
        HotelReportRow? cheapest,
        HotelReportRow transport,
        IReadOnlyList<HotelReportRow> rows,
        IReadOnlyList<string> allowed,
        IReadOnlyList<string> chosen)
    {
        var sentences = new List<string>();
        switch (strategy)
        {
            case RecommendationStrategy.BudgetPriority:
                sentences.Add($"{selected.Name}, girilen gecelik tutarı en düşük seçenek ({RecommendationMetricFormatter.Price(selected.Price)}).");
                AddRouteFacts(sentences, selected);
                AddPriceTravelTradeoff(sentences, strategy, selected, transport, cheapest);
                break;
            case RecommendationStrategy.TransportPriority:
                if (selected.DistanceKind == DistanceKind.Road)
                {
                    sentences.Add($"{selected.Name}, ölçülen yol rotaları içinde en kısa tahmini yolculuk süresine sahip ({RecommendationMetricFormatter.RoadDuration(selected)}; {RecommendationMetricFormatter.Distance(selected.DistanceMeters)}).");
                }
                else
                {
                    sentences.Add($"Ölçülmüş yol rotası bulunmadığı için ulaşım karşılaştırması düz çizgi tahminine dayanıyor. {selected.Name} için bu tahmin {RecommendationMetricFormatter.Distance(selected.DistanceMeters)}.");
                }
                AddPriceTravelTradeoff(sentences, strategy, selected, transport, cheapest);
                break;
            case RecommendationStrategy.Balanced:
                sentences.Add($"{selected.Name}, en düşük girilen gecelik tutarın %10 üzerindeki bütçe aralığında ulaşım süresi en iyi seçenektir ({RecommendationMetricFormatter.Price(selected.Price)}).");
                AddRouteFacts(sentences, selected);
                AddPriceTravelTradeoff(sentences, strategy, selected, transport, cheapest);
                break;
        }

        if (allowed.Contains("walking-route-available", StringComparer.Ordinal) && selected.WalkingMetrics is { } walking)
        {
            sentences.Add($"Ölçülmüş yürüyüş rotası ayrıca {RecommendationMetricFormatter.Distance(walking.DistanceMeters)} ve {RecommendationMetricFormatter.WalkingDuration(walking)} sürüyor.");
        }
        else if (selected.WalkingRouteRequested && selected.WalkingMetrics is null)
        {
            sentences.Add("Bu otel için yürüyüş rotası ölçülemedi.");
        }

        if (allowed.Contains("shortest-road-distance-tiebreak", StringComparer.Ordinal)
            && chosen.Contains("shortest-road-distance-tiebreak", StringComparer.Ordinal))
        {
            sentences.Add("Tahmini yol süreleri eşit olduğundan daha kısa yol mesafesi belirleyici oldu.");
        }

        return string.Join(' ', sentences);
    }

    private static void AddRouteFacts(List<string> sentences, HotelReportRow selected)
    {
        if (selected.DistanceKind == DistanceKind.Road)
        {
            sentences.Add($"Ölçülen yol rotası {RecommendationMetricFormatter.Distance(selected.DistanceMeters)} ve tabloda gösterildiği gibi {RecommendationMetricFormatter.RoadDuration(selected)}.");
        }
        else
        {
            sentences.Add($"Ölçülmüş yol rotası yok; düz çizgi tahmini {RecommendationMetricFormatter.Distance(selected.DistanceMeters)} (yol mesafesi değildir).");
        }
    }

    private static void AddPriceTravelTradeoff(
        List<string> sentences,
        RecommendationStrategy strategy,
        HotelReportRow selected,
        HotelReportRow transport,
        HotelReportRow? cheapest)
    {
        if (cheapest is null)
        {
            return;
        }

        var lowest = cheapest.Price.Amount;
        var transportGapIsSmall = lowest == 0
            ? transport.Price.Amount == 0
            : transport.Price.Amount - lowest <= lowest * BalancePremiumLimit;
        var transportGapText = PriceGapText(cheapest, transport);
        var selectedGapText = PriceGapText(cheapest, selected);
        if (strategy == RecommendationStrategy.BudgetPriority)
        {
            if (selected.Name == transport.Name)
            {
                sentences.Add("Bu otel hem en düşük girilen tutara hem de ulaşım önceliğine göre ilk sıraya sahiptir.");
            }
            else if (transportGapIsSmall)
            {
                sentences.Add($"Ulaşım öncelikli {transport.Name}, en düşük tutardan {transportGapText} daha pahalıdır. {TravelTradeoffText(cheapest, transport)}");
            }
            else
            {
                sentences.Add($"Bu seçenek ulaşım öncelikli {transport.Name} karşısında daha düşük tutarlıdır ({RecommendationMetricFormatter.Price(cheapest.Price)}). {TravelTradeoffText(cheapest, transport)}");
            }
            return;
        }

        if (strategy == RecommendationStrategy.TransportPriority)
        {
            if (selected.Name == cheapest.Name)
            {
                sentences.Add("Bu otel hem en düşük girilen tutara hem de ulaşım önceliğine göre ilk sıraya sahiptir.");
            }
            else if (transportGapIsSmall)
            {
                sentences.Add($"Fiyat farkı en ucuz seçeneğe göre {transportGapText}. {TravelTradeoffText(cheapest, transport)}");
            }
            else
            {
                sentences.Add($"En ucuz seçenek {cheapest.Name} ({RecommendationMetricFormatter.Price(cheapest.Price)}); aradaki fiyat farkı {transportGapText}. {TravelTradeoffText(cheapest, transport)}");
            }
        }
        else if (selected.Name == cheapest.Name)
        {
            if (transport.Name != cheapest.Name)
            {
                sentences.Add($"Ulaşım öncelikli {transport.Name} {transportGapText} daha pahalıdır. {TravelTradeoffText(cheapest, transport)}");
            }
        }
        else
        {
            if (selected.Name == transport.Name)
            {
                sentences.Add($"Bu denge seçeneği aynı zamanda ulaşım süresinde ilk sıradadır; en ucuz seçeneğe göre fiyat farkı {selectedGapText}. {TravelTradeoffText(cheapest, selected)}");
            }
            else
            {
                sentences.Add($"Bu otel en düşük tutardan {selectedGapText} daha pahalıdır ve denge seçeneğinin %10 fiyat sınırında kalır. {TravelTradeoffText(selected, transport)}");
            }
        }
    }

    private static string TravelTradeoffText(HotelReportRow cheaper, HotelReportRow transport)
    {
        if (cheaper.DistanceKind != transport.DistanceKind)
        {
            return $"{transport.Name} için ölçülmüş yol rotası vardır; {cheaper.Name} için yalnızca düz çizgi tahmini bulunduğundan yol süreleri doğrudan karşılaştırılamaz.";
        }

        if (cheaper.DistanceKind == DistanceKind.StraightLine)
        {
            var difference = Math.Abs(cheaper.DistanceMeters - transport.DistanceMeters);
            return difference == 0
                ? "İki otelin düz çizgi tahminleri eşittir; yol süresi ölçülmemiştir."
                : $"{transport.Name} düz çizgi tahmini {RecommendationMetricFormatter.Distance(difference)} daha kısadır; bu yol mesafesi veya yolculuk süresi değildir.";
        }

        var cheaperMinutes = Math.Max(1, cheaper.DurationSeconds / 60);
        var transportMinutes = Math.Max(1, transport.DurationSeconds / 60);
        if (cheaperMinutes > transportMinutes)
        {
            return $"{cheaper.Name} için tabloda gösterilen yol süresi {cheaperMinutes - transportMinutes} dk daha uzundur.";
        }
        if (cheaperMinutes < transportMinutes)
        {
            return $"{cheaper.Name} için tabloda gösterilen yol süresi {transportMinutes - cheaperMinutes} dk daha kısadır.";
        }

        return $"İki yol süresi de PDF tablosunda {cheaperMinutes} dk olarak gösterilir; dakika yuvarlamasında süre farkı görünmez.";
    }

    private static HotelReportRow BestTransport(IEnumerable<HotelReportRow> rows) => rows
        .OrderBy(row => row.DistanceKind)
        .ThenBy(row => row.DistanceKind == DistanceKind.Road ? row.DurationSeconds : 0)
        .ThenBy(row => row.DistanceMeters)
        .ThenBy(row => row.Name, StringComparer.Ordinal)
        .First();

    private static string FormatPercent(decimal fraction) =>
        string.Create(CultureInfo.InvariantCulture, $"{fraction * 100m:0.#}");

    private static string PriceGapText(HotelReportRow cheapest, HotelReportRow other) => cheapest.Price.Amount == 0
        ? $"{RecommendationMetricFormatter.Price(other.Price)} tutarında (en düşük tutar sıfır olduğu için yüzde farkı hesaplanamaz)"
        : $"%{FormatPercent(decimal.Max(0m, other.Price.Amount - cheapest.Price.Amount) / cheapest.Price.Amount)}";

    private sealed record OptionSeed(
        RecommendationStrategy Strategy,
        HotelReportRow? Hotel,
        IReadOnlyList<string> AllowedReasonCodes,
        string Explanation);
}
