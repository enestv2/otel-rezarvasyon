using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Domain.Abstractions;

/// <summary>
/// Rapor modelini indirilebilir belgeye çevirir (R-1). Uygulama Infrastructure'dadır (ADR-0006);
/// çekirdek PDF kütüphanesini bilmez.
/// </summary>
public interface IRecommendationReportRenderer
{
    Task<ReportDocument> RenderAsync(RecommendationReport report, CancellationToken cancellationToken);
}