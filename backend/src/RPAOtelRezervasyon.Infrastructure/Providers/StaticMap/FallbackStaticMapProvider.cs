using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;

public sealed class FallbackStaticMapProvider(
    HostedStaticMapProvider primary,
    SchematicStaticMapProvider fallback) : IStaticMapProvider
{
    public string ProviderId => HostedStaticMapProvider.Id;

    public async Task<StaticMapOutcome> RenderAsync(StaticMapRequest request, CancellationToken cancellationToken)
    {
        var outcome = await primary.RenderAsync(request, cancellationToken);
        if (outcome.Status == OutcomeStatus.Found)
        {
            return outcome;
        }

        var fallbackOutcome = await fallback.RenderAsync(request, cancellationToken);
        return fallbackOutcome.Status == OutcomeStatus.Found && outcome.Attribution is not null
            ? StaticMapOutcome.Found(fallbackOutcome.ProviderId, fallbackOutcome.Image!.Value, fallbackOutcome.ContentType!, outcome.Attribution,
                fallbackOutcome.IncludesDetailView)
            : fallbackOutcome;
    }
}
