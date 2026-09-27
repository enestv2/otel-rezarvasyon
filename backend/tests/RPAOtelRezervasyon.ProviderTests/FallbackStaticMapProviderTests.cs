using System.Net;
using Microsoft.Extensions.Options;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;
using RPAOtelRezervasyon.ProviderTests.Support;

namespace RPAOtelRezervasyon.ProviderTests;

public sealed class FallbackStaticMapProviderTests
{
    [Fact]
    public async Task RenderAsync_uses_schematic_map_when_primary_is_unavailable()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var client = new HttpClient(handler);
        var primary = new HostedStaticMapProvider(client, Options.Create(new StaticMapOptions { ApiKey = "test-key" }));
        var provider = new FallbackStaticMapProvider(primary, new SchematicStaticMapProvider());

        var outcome = await provider.RenderAsync(new StaticMapRequest(
            new GeoPoint(41, 29),
            [new StaticMapMarker("Otel", new GeoPoint(41.1, 29.1), true)],
            320,
            180), CancellationToken.None);

        Assert.Equal(OutcomeStatus.Found, outcome.Status);
        Assert.Equal(SchematicStaticMapProvider.Id, outcome.ProviderId);
        Assert.Null(outcome.Attribution);
    }
}
