using System.Threading.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.RateLimiting;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Infrastructure.Providers.Common.Options;
using RPAOtelRezervasyon.Infrastructure.Providers.Common.Resilience;
using RPAOtelRezervasyon.Infrastructure.Providers.Geoapify;

namespace RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;

/// <summary>Şematik coğrafi görsel sağlayıcısının kaydı (ADR-0006).</summary>
public static class SchematicStaticMapServiceCollectionExtensions
{
    public static IServiceCollection AddSchematicStaticMap(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IStaticMapProvider, SchematicStaticMapProvider>();

        return services;
    }

    public static IServiceCollection AddStaticMapWithFallback(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddValidatedOptions<StaticMapOptions>(configuration, StaticMapOptions.SectionName);
        services.PostConfigure<StaticMapOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                options.ApiKey = configuration[$"{GeoapifyOptions.SectionName}:ApiKey"] ?? string.Empty;
            }
        });
        services.AddLogging(logging => logging.AddFilter(
            "System.Net.Http.HttpClient.HostedStaticMapProvider",
            LogLevel.None));
        services.AddKeyedSingleton<RateLimiter>(HostedStaticMapProvider.Id, (serviceProvider, _) =>
            ResiliencePolicies.CreateProviderRateLimiter(
                serviceProvider.GetRequiredService<IOptions<StaticMapOptions>>().Value.RequestsPerSecond));
        services.AddHttpClient<HostedStaticMapProvider>((_, client) => client.Timeout = Timeout.InfiniteTimeSpan)
            .AddResilienceHandler("geoapify-static-map-resilience", (builder, context) =>
            {
                var options = context.ServiceProvider.GetRequiredService<IOptions<StaticMapOptions>>().Value;
                var limiter = context.ServiceProvider.GetRequiredKeyedService<RateLimiter>(HostedStaticMapProvider.Id);
                builder
                    .AddRateLimiter(ResiliencePolicies.CreateRateLimiterStrategy(limiter))
                    .AddTimeout(TimeSpan.FromSeconds(20))
                    .AddRetry(ResiliencePolicies.CreateRetry(options.RetryMaxAttempts))
                    .AddCircuitBreaker(ResiliencePolicies.CreateCircuitBreaker())
                    .AddTimeout(TimeSpan.FromSeconds(10));
            });
        services.AddSingleton<SchematicStaticMapProvider>();
        services.AddTransient<FallbackStaticMapProvider>();
        services.AddTransient<IStaticMapProvider>(provider => provider.GetRequiredService<FallbackStaticMapProvider>());

        return services;
    }
}
