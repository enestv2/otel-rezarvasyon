using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RPAOtelRezervasyon.Api.Contracts;
using RPAOtelRezervasyon.Application.Modules.Reporting;
using RPAOtelRezervasyon.Application.Orchestration;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Domain.Validation;

namespace RPAOtelRezervasyon.Api.Controllers;

/// <summary>
/// Otel önerisi uç noktaları. Varsayılan politika kimlik doğrulaması ister (AC-9); durum eşlemesi
/// docs/conventions.md'ye göredir: doğrulama → 400, çözümlenemeyen etkinlik alanı/otel → 422,
/// sağlayıcı erişilemezliği → 502. JSON ve PDF uç noktaları **aynı** girdiyi ve aynı hata
/// davranışını paylaşır (0006, G-3/G-4).
/// </summary>
[ApiController]
[Authorize]
[Route("api/recommendations")]
public sealed class RecommendationsController(
    HotelRecommendationOrchestrator orchestrator,
    RecommendationReportService reportService) : ControllerBase
{
    /// <summary>İstek gövdesi üst sınırı: en fazla 20 otel taşır; büyük gövdeler bağlamadan reddedilir (DoS).</summary>
    private const int MaxRequestBytes = 64_000;

    [HttpPost]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(RecommendationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> RecommendAsync(
        [FromBody] RecommendationRequestDto request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await RunAsync(request, cancellationToken).ConfigureAwait(false);

        return ToFailure(result) ?? Ok(ToResponse(result));
    }

    /// <summary>
    /// Aynı girdiyle öneriyi coğrafi görselli bir PDF belgesi olarak döndürür (R-1..R-8). Hata
    /// durumlarında PDF üretilmez; JSON uç noktasıyla aynı ProblemDetails gövdesi döner.
    /// </summary>
    [HttpPost("pdf")]
    [RequestSizeLimit(MaxRequestBytes)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> RecommendPdfAsync(
        [FromBody] RecommendationRequestDto request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await RunAsync(request, cancellationToken).ConfigureAwait(false);

        if (ToFailure(result) is { } failure)
        {
            return failure;
        }

        var venueName = result.VenueName
            ?? throw new InvalidOperationException("Başarılı sonuçta etkinlik alanı adı bulunmalıdır.");
        var document = await reportService.CreateAsync(
            venueName,
            result,
            new PersonnelInfo(request.PersonnelRegistrationNumber, request.PersonnelFirstName, request.PersonnelLastName),
            cancellationToken).ConfigureAwait(false);

        return File(document.Content.ToArray(), document.ContentType, document.FileName);
    }

    private async Task<RecommendationResult> RunAsync(
        RecommendationRequestDto request,
        CancellationToken cancellationToken)
    {
        var domainRequest = new RecommendationRequest(
            request.VenueName ?? string.Empty,
            request.Hotels?
                .Select(hotel => new RequestedHotel(hotel?.Name, hotel?.Price?.Amount, hotel?.Price?.Currency))
                .ToArray() ?? [],
            new PersonnelInfo(request.PersonnelRegistrationNumber, request.PersonnelFirstName, request.PersonnelLastName),
            new GeocodingContext(request.EventCity, request.EventCountry));

        return await orchestrator.RecommendAsync(domainRequest, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Başarısız durumu uygun hata yanıtına çevirir; başarılıysa <c>null</c> döner.</summary>
    private IActionResult? ToFailure(RecommendationResult result) => result.Status switch
    {
        RecommendationStatus.Succeeded => null,
        RecommendationStatus.InvalidRequest => ToValidationProblem(result),
        RecommendationStatus.VenueNotResolved => Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "Etkinlik alanı çözümlenemedi",
            detail: "Etkinlik alanı adından konum bulunamadı; öneri üretilmedi."),
        RecommendationStatus.NoResolvedHotels => NoResolvedHotelsProblem(result),
        RecommendationStatus.ProviderUnavailable => Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "Konum sağlayıcısına erişilemedi",
            detail: "Etkinlik alanı için konum sağlayıcısı isteği başarısız oldu; öneri üretilmedi."),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    private ActionResult ToValidationProblem(RecommendationResult result)
    {
        var modelState = new ModelStateDictionary();

        foreach (var issue in result.Validation.Issues)
        {
            modelState.AddModelError(issue.Code, issue.Message);
        }

        return ValidationProblem(modelState);
    }

    private ObjectResult NoResolvedHotelsProblem(RecommendationResult result)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Hiçbir otel çözümlenemedi",
            Detail = "Otel adlarından hiçbiri konuma çevrilemedi; öneri üretilmedi.",
        };
        problemDetails.Extensions["unresolvedHotels"] = result.UnresolvedHotels;

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status422UnprocessableEntity,
            ContentTypes = { "application/problem+json" },
        };
    }

    private static RecommendationResponseDto ToResponse(RecommendationResult result)
    {
        var recommendation = result.Recommendation
            ?? throw new InvalidOperationException("Başarılı sonuçta öneri bulunmalıdır.");
        var rationale = result.Rationale
            ?? throw new InvalidOperationException("Başarılı sonuçta gerekçe bulunmalıdır.");

        return new RecommendationResponseDto(
            result.Status.ToString(),
            ToDto(recommendation.Selected),
            new RationaleDto(
                rationale.HotelName,
                rationale.DurationSeconds,
                rationale.DistanceMeters,
                rationale.DistanceKind.ToString(),
                rationale.EvaluatedHotelCount),
            recommendation.RankedHotels.Select(ToDto).ToArray(),
            result.UnresolvedHotels);
    }

    private static RankedHotelDto ToDto(HotelCandidate candidate) => new(
        candidate.Hotel.Name,
        new PriceDto(candidate.Hotel.Price.Amount, candidate.Hotel.Price.Currency),
        candidate.Location.Latitude,
        candidate.Location.Longitude,
        candidate.Metrics.DistanceMeters,
        candidate.Metrics.DurationSeconds,
        candidate.Metrics.Kind.ToString());
}
