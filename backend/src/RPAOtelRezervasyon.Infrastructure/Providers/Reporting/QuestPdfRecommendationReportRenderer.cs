using System.Globalization;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Infrastructure.Providers.Reporting;

/// <summary>
/// Rapor modelini QuestPDF ile Türkçe şablonlu PDF belgesine çevirir (ADR-0006, R-1..R-9). Metinler
/// çekirdekten gelir; bu adaptör iş kuralı içermez, yalnızca yerleşim ve biçimlendirme yapar.
/// Biçimlendirme kültürden bağımsızdır (docs/conventions.md).
/// </summary>
public sealed class QuestPdfRecommendationReportRenderer : IRecommendationReportRenderer
{
    private const string FontFamily = "Lato";

    public Task<ReportDocument> RenderAsync(RecommendationReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = Document.Create(container => Compose(container, report)).GeneratePdf();

        return Task.FromResult(new ReportDocument(bytes, ReportDocument.PdfContentType, BuildFileName(report.VenueName)));
    }

    private static void Compose(IDocumentContainer container, RecommendationReport report)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(style => style.FontFamily(FontFamily).FontSize(10).FontColor(Colors.Grey.Darken4));

            page.Header().Element(element => ComposeHeader(element, report));

            page.Content().PaddingVertical(12).Column(column =>
            {
                column.Spacing(14);
                column.Item().Element(element => ComposeRecommendationOptions(element, report));

                if (report.Map is not null)
                {
                    column.Item().Element(element => ComposeMap(element, report.Map));
                }
                else
                {
                    column.Item().Text("Coğrafi görsel bu belge için üretilemedi.").Italic().FontColor(Colors.Grey.Darken1);
                }

                column.Item().Element(element => ComposeTable(element, report));

                if (report.UnresolvedHotels.Count > 0)
                {
                    column.Item().Element(element => ComposeUnresolved(element, report));
                }
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Sayfa ").FontSize(8).FontColor(Colors.Grey.Darken1);
                text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                text.Span(" / ").FontSize(8).FontColor(Colors.Grey.Darken1);
                text.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static void ComposeHeader(IContainer container, RecommendationReport report)
    {
        container.Column(column =>
        {
            column.Item().Text(report.Title).FontSize(20).SemiBold();
            column.Item().Text(report.VenueName).FontSize(12).FontColor(Colors.Grey.Darken2);
            if (report.Personnel is { } personnel)
            {
                column.Item().Text($"Talep sahibi: {personnel.FirstName} {personnel.LastName} (Sicil: {personnel.RegistrationNumber})")
                    .FontSize(9);
            }
            column.Item().Text(string.Create(CultureInfo.InvariantCulture, $"Belge üretim zamanı: {report.GeneratedAtUtc.UtcDateTime:yyyy-MM-dd HH:mm} UTC"))
                .FontSize(8)
                .FontColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeRecommendationOptions(IContainer container, RecommendationReport report)
    {
        container.Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("OTEL SEÇENEKLERİ").FontSize(11).SemiBold().FontColor(Colors.Orange.Darken3);
            foreach (var option in report.Options)
            {
                column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(9).Column(card =>
                {
                    card.Spacing(3);
                    card.Item().Text(StrategyLabel(option.Strategy)).FontSize(9).SemiBold().FontColor(Colors.Orange.Darken3);
                    if (option.Hotel is { } hotel)
                    {
                        card.Item().Text(hotel.Name).FontSize(14).SemiBold();
                        card.Item().Text($"Mesafe: {RecommendationMetricFormatter.Distance(hotel.DistanceMeters)} · Süre: {RecommendationMetricFormatter.RoadDuration(hotel)} · Mesafe türü: {FormatKind(hotel.DistanceKind)} · Gecelik ücret: {RecommendationMetricFormatter.Price(hotel.Price)}")
                            .FontSize(9);
                    }
                    card.Item().Text(option.Explanation).FontSize(9);
                });
            }
            column.Item().Text("Fiyatlar istekte iletilen gecelik tutarlardır; rezervasyon teklifi veya teyidi değildir.")
                .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
        });
    }

    private static string StrategyLabel(RecommendationStrategy strategy) => strategy switch
    {
        RecommendationStrategy.BudgetPriority => "BÜTÇE ÖNCELİKLİ",
        RecommendationStrategy.TransportPriority => "ULAŞIM ÖNCELİKLİ",
        RecommendationStrategy.Balanced => "DENGE",
        _ => throw new ArgumentOutOfRangeException(nameof(strategy)),
    };

    private static void ComposeMap(IContainer container, StaticMapImage map)
    {
        container.Column(column =>
        {
            column.Spacing(6);
            column.Item().Text("HARİTA GÖRÜNÜMÜ").FontSize(11).SemiBold().FontColor(Colors.Grey.Darken2);
            column.Item().Image(map.Content.ToArray()).FitWidth();
            column.Item().Element(legend => ComposeMapLegend(legend, map));

            if (!string.IsNullOrWhiteSpace(map.Attribution))
            {
                column.Item().Text(map.Attribution!).FontSize(8).FontColor(Colors.Grey.Darken1);
            }
        });
    }

    private static void ComposeMapLegend(IContainer container, StaticMapImage map)
    {
        container.Column(column =>
        {
            column.Spacing(4);
            column.Item().Row(row =>
            {
                row.Spacing(12);
                LegendItem(row, "#DC2626", "Varış: etkinlik alanı", LegendSymbol.DestinationPin);
                LegendItem(row, Colors.Orange.Darken2, "Ulaşım öncelikli rota", LegendSymbol.SolidLine);
                LegendItem(row, Colors.Grey.Darken2, "Kesikli: diğer otel rotaları", LegendSymbol.DashedLine);
                row.AutoItem().Text("Rota rengi = otel işareti").FontSize(8);
                LegendItem(row, Colors.Grey.Darken4, "Numara: PDF sırası", LegendSymbol.Number);
            });
            column.Item().Text(MapLegend(map)).FontSize(8.5f).FontColor(Colors.Grey.Darken1);
        });
    }

    private enum LegendSymbol
    {
        Square,
        DestinationPin,
        SolidLine,
        DashedLine,
        Number,
    }

    private static void LegendItem(RowDescriptor row, string color, string label, LegendSymbol symbol)
    {
        row.AutoItem().Row(item =>
        {
            item.Spacing(4);
            item.AutoItem().Element(container => DrawLegendSymbol(container, color, symbol));
            item.AutoItem().Text(label).FontSize(8);
        });
    }

    private static void DrawLegendSymbol(IContainer container, string color, LegendSymbol symbol)
    {
        switch (symbol)
        {
            case LegendSymbol.Square:
                container.Width(8).Height(8).Background(color);
                break;
            case LegendSymbol.DestinationPin:
                container.Width(8).Height(11).Column(column =>
                {
                    column.Item().AlignCenter().Width(8).Height(8).Background(color).CornerRadius(4);
                    column.Item().AlignCenter().Width(2).Height(3).Background(color);
                });
                break;
            case LegendSymbol.SolidLine:
                container.Width(18).Height(3).Background(color);
                break;
            case LegendSymbol.DashedLine:
                container.Width(18).Height(2).Row(row =>
                {
                    row.Spacing(2);
                    row.ConstantItem(4).Height(2).Background(color);
                    row.ConstantItem(4).Height(2).Background(color);
                    row.ConstantItem(4).Height(2).Background(color);
                });
                break;
            case LegendSymbol.Number:
                container.Width(12).Height(12).Background(color).AlignCenter().AlignMiddle()
                    .Text("1").FontSize(7).SemiBold().FontColor(Colors.White);
                break;
        }
    }

    private static string MapLegend(StaticMapImage map)
    {
        var source = map.Attribution is null ? "Şematik görünüm. " : string.Empty;
        return $"{source}Numaralar otel tablosundaki sırasını gösterir. Renkli bağlantı ucu otelin gerçek konumudur; rozet konumda kalıyorsa rozet merkezi konumdur. Yalnızca sağlayıcıdan alınan yol geometrileri çizilir; geometri yoksa rota çizgisi gösterilmez.";
    }
    private static void ComposeTable(IContainer container, RecommendationReport report)
    {
        container.Column(column =>
        {
            column.Spacing(4);
            column.Item().Text("Değerlendirilen oteller (sıralı)").FontSize(11).SemiBold();

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(24);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.25f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    GroupHeaderCell(header, "OTEL", 2);
                    GroupHeaderCell(header, "YOL", 3);
                    GroupHeaderCell(header, "YÜRÜME", 2);
                    GroupHeaderCell(header, "GECELİK ÜCRET", 1);
                    HeaderCell(header, "#");
                    HeaderCell(header, "Otel");
                    HeaderCell(header, "Mesafe");
                    HeaderCell(header, "Süre");
                    HeaderCell(header, "Tür");
                    HeaderCell(header, "Mesafe");
                    HeaderCell(header, "Süre");
                    HeaderCell(header, "Tutar");
                });

                for (var index = 0; index < report.RankedHotels.Count; index++)
                {
                    var row = report.RankedHotels[index];

                    var alternate = index % 2 == 1;
                    var optionLabels = string.Join(", ", report.Options
                        .Where(option => option.Hotel?.Name == row.Name)
                        .Select(option => StrategyLabel(option.Strategy)));
                    var hotelLabel = string.IsNullOrEmpty(optionLabels) ? row.Name : $"{row.Name} ({optionLabels})";
                    BodyCell(table, string.Create(CultureInfo.InvariantCulture, $"{index + 1}"), row.IsSelected, alternate);
                    BodyCell(table, hotelLabel, row.IsSelected, alternate);
                    BodyCell(table, FormatDistance(row.DistanceMeters), row.IsSelected, alternate);
                    BodyCell(table, FormatDuration(row), row.IsSelected, alternate);
                    BodyCell(table, FormatKind(row.DistanceKind), row.IsSelected, alternate);
                    BodyCell(table, FormatWalkingDistance(row), row.IsSelected, alternate);
                    BodyCell(table, FormatWalkingDuration(row), row.IsSelected, alternate);
                    BodyCell(table, FormatPrice(row.Price), row.IsSelected, alternate);
                }
            });
        });
    }

    private static void ComposeUnresolved(IContainer container, RecommendationReport report)
    {
        container.Column(column =>
        {
            column.Spacing(2);
            column.Item().Text("Çözümlenemeyen oteller").FontSize(11).SemiBold();
            column.Item().Text(string.Join(", ", report.UnresolvedHotels)).FontSize(9).FontColor(Colors.Grey.Darken1);
            column.Item().Text("Bu oteller konuma çevrilemedi veya mesafesi ölçülemedi; öneri dışında bırakıldı.").FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    private static void GroupHeaderCell(TableCellDescriptor header, string text, int span) =>
        header.Cell().ColumnSpan((uint)span).Background(Colors.Blue.Darken3).Padding(5)
            .Text(text).FontSize(8).SemiBold().FontColor(Colors.White);

    private static void HeaderCell(TableCellDescriptor header, string text) =>
        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(text).FontSize(9).SemiBold();

    private static void BodyCell(TableDescriptor table, string text, bool highlighted, bool alternate)
    {
        IContainer cell = table.Cell();

        if (highlighted)
        {
            cell = cell.Background(Colors.Orange.Lighten5);
        }
        else if (alternate)
        {
            cell = cell.Background(Colors.Grey.Lighten5);
        }

        cell.Padding(5).Text(text).FontSize(9);
    }

    private static string FormatDistance(int meters) => RecommendationMetricFormatter.Distance(meters);

    private static string FormatDuration(HotelReportRow row) => RecommendationMetricFormatter.RoadDuration(row);

    private static string FormatWalkingDistance(HotelReportRow row) =>
        !row.WalkingRouteRequested ? "—"
        : row.WalkingMetrics is null ? "Alınamadı"
        : RecommendationMetricFormatter.Distance(row.WalkingMetrics.DistanceMeters);

    private static string FormatWalkingDuration(HotelReportRow row) =>
        !row.WalkingRouteRequested ? "—"
        : row.WalkingMetrics is null ? "Alınamadı"
        : RecommendationMetricFormatter.WalkingDuration(row.WalkingMetrics);

    private static string FormatKind(DistanceKind kind) =>
        kind == DistanceKind.Road ? "Yol" : "Düz çizgi";

    private static string FormatPrice(NightlyPrice price) => RecommendationMetricFormatter.Price(price);

    /// <summary>Etkinlik alanı adından ASCII-güvenli, kültürden bağımsız bir dosya adı üretir.</summary>
    private static string BuildFileName(string venueName)
    {
        var builder = new StringBuilder("oneri-");

        foreach (var character in venueName.Trim().ToLowerInvariant())
        {
            var mapped = character switch
            {
                'ı' => 'i',
                'ş' => 's',
                'ğ' => 'g',
                'ç' => 'c',
                'ö' => 'o',
                'ü' => 'u',
                >= 'a' and <= 'z' => character,
                >= '0' and <= '9' => character,
                _ => '-',
            };

            if (mapped == '-' && (builder.Length == 0 || builder[^1] == '-'))
            {
                continue;
            }

            builder.Append(mapped);
        }

        var slug = builder.ToString().TrimEnd('-');
        if (slug == "oneri")
        {
            slug = "oneri-belgesi";
        }

        return $"{slug}.pdf";
    }
}
