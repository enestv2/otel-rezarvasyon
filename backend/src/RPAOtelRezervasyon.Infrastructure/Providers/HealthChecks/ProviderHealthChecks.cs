using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RPAOtelRezervasyon.Infrastructure.Providers.Geoapify;

namespace RPAOtelRezervasyon.Infrastructure.Providers.HealthChecks;

/// <summary>
/// Sağlayıcı erişilebilirliğini raporlayan sağlık kontrolleri (docs/architecture.md). Sağlayıcı
/// erişilemezse servis yine çalışır (önbellek/fallback); bu yüzden sonuç <c>Degraded</c>'dir,
/// <c>Unhealthy</c> değil.
/// </summary>
public static class ProviderHealthChecks
{
    public const string ProviderTag = "provider";

    public static IServiceCollection AddProviderHealthChecks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient("geoapify-health", client => client.Timeout = TimeSpan.FromSeconds(5));

        services.AddHealthChecks()
            .AddCheck<GeoapifyHealthCheck>("geoapify", tags: [ProviderTag]);

        return services;
    }
}

/// <summary>Geoapify API host erişilebilirliğini anahtarsız ve kredi tüketmeden kontrol eder.</summary>
public sealed class GeoapifyHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptions<GeoapifyOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            using var response = await httpClientFactory.CreateClient("geoapify-health")
                .GetAsync(options.Value.BaseUrl, cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("Geoapify API host yanıt veriyor.");
        }
        catch (HttpRequestException)
        {
            return HealthCheckResult.Degraded("Geoapify API host erişilemiyor.");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Degraded("Geoapify health check zaman aşımına uğradı.");
        }
    }
}

