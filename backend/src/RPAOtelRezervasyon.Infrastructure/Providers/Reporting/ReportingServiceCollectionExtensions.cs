using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using RPAOtelRezervasyon.Domain.Abstractions;

namespace RPAOtelRezervasyon.Infrastructure.Providers.Reporting;

/// <summary>PDF rapor renderer kaydı ve QuestPDF lisans ayarı (ADR-0006).</summary>
public static class ReportingServiceCollectionExtensions
{
    public static IServiceCollection AddQuestPdfReporting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        QuestPDF.Settings.License = LicenseType.Community;
        services.AddSingleton<IRecommendationReportRenderer, QuestPdfRecommendationReportRenderer>();

        return services;
    }
}