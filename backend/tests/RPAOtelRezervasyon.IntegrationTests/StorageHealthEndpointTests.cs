using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RPAOtelRezervasyon.IntegrationTests.Support;

namespace RPAOtelRezervasyon.IntegrationTests;

public sealed class StorageHealthEndpointTests
{
    [Fact]
    public async Task Storage_health_reports_mongo_separately_from_provider_health()
    {
        using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var registrations = factory.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations;

        Assert.Contains(registrations, registration =>
            registration.Name == "mongo-geocode-cache" && registration.Tags.Contains("storage"));
        Assert.Contains(registrations, registration =>
            registration.Name == "geoapify" && registration.Tags.Contains("provider"));

        using var response = await client.GetAsync("/health/storage");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Degraded", body, StringComparison.Ordinal);
    }
}
