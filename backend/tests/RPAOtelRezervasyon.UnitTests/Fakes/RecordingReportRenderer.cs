using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.UnitTests.Fakes;

/// <summary>Renderer'a giden deterministik rapor modelini yakalayan test çifti.</summary>
public sealed class RecordingReportRenderer : IRecommendationReportRenderer
{
    public int CallCount { get; private set; }

    public RecommendationReport? LastReport { get; private set; }

    public ReportDocument Document { get; set; } =
        new(new byte[] { 37, 80, 68, 70 }, ReportDocument.PdfContentType, "oneri.pdf");

    public Task<ReportDocument> RenderAsync(RecommendationReport report, CancellationToken cancellationToken)
    {
        CallCount++;
        LastReport = report;

        return Task.FromResult(Document);
    }
}