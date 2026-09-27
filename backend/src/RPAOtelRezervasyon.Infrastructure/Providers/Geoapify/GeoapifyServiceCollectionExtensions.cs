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

namespace RPAOtelRezervasyon.Infrastructure.Providers.Geoapify;

public static class GeoapifyServiceCollectionExtensions
{
    public static IServiceCollection AddGeoapifyProviders(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<GeoapifyOptions>()
            .Bind(configuration.GetSection(GeoapifyOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    options.ApiKey = configuration["StaticMap:ApiKey"] ?? string.Empty;
                }
            })
            .ValidateDataAnnotations()
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey),
                "Geoapify:ApiKey zorunludur.")
            .ValidateOnStart();

        services.AddKeyedSingleton<RateLimiter>(GeoapifyGeocodingProvider.Id, (provider, _) =>
            ResiliencePolicies.CreateProviderRateLimiter(provider.GetRequiredService<IOptions<GeoapifyOptions>>().Value.RequestsPerSecond));
        services.AddKeyedSingleton<RateLimiter>(GeoapifyRouteDistanceProvider.Id, (provider, _) =>
            ResiliencePolicies.CreateProviderRateLimiter(provider.GetRequiredService<IOptions<GeoapifyOptions>>().Value.RequestsPerSecond));

        services.AddHttpClient<GeoapifyGeocodingProvider>((provider, client) =>
            {
                client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<GeoapifyOptions>>().Value.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddResilienceHandler("geoapify-geocoding-resilience", (builder, context) =>
            {
                var options = context.ServiceProvider.GetRequiredService<IOptions<GeoapifyOptions>>().Value;
                var limiter = context.ServiceProvider.GetRequiredKeyedService<RateLimiter>(GeoapifyGeocodingProvider.Id);
                builder.AddRateLimiter(ResiliencePolicies.CreateRateLimiterStrategy(limiter))
                    .AddTimeout(TimeSpan.FromSeconds(30))
                    .AddRetry(ResiliencePolicies.CreateRetry(options.RetryMaxAttempts))
                    .AddCircuitBreaker(ResiliencePolicies.CreateCircuitBreaker())
                    .AddTimeout(TimeSpan.FromSeconds(10));
            });

        services.AddHttpClient<GeoapifyRouteDistanceProvider>((provider, client) =>
            {
                client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<GeoapifyOptions>>().Value.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddResilienceHandler("geoapify-routing-resilience", (builder, context) =>
            {
                var options = context.ServiceProvider.GetRequiredService<IOptions<GeoapifyOptions>>().Value;
                var limiter = context.ServiceProvider.GetRequiredKeyedService<RateLimiter>(GeoapifyRouteDistanceProvider.Id);
                builder.AddRateLimiter(ResiliencePolicies.CreateRateLimiterStrategy(limiter))
                    .AddTimeout(TimeSpan.FromSeconds(30))
                    .AddRetry(ResiliencePolicies.CreateRetry(options.RetryMaxAttempts))
                    .AddCircuitBreaker(ResiliencePolicies.CreateCircuitBreaker())
                    .AddTimeout(TimeSpan.FromSeconds(10));
            });

        services.AddLogging(logging =>
        {
            logging.AddFilter("System.Net.Http.HttpClient.GeoapifyGeocodingProvider", LogLevel.None);
            logging.AddFilter("System.Net.Http.HttpClient.GeoapifyRouteDistanceProvider", LogLevel.None);
        });
        services.AddTransient<IGeocodingProvider>(provider => provider.GetRequiredService<GeoapifyGeocodingProvider>());
        services.AddTransient<IRouteDistanceProvider>(provider => provider.GetRequiredService<GeoapifyRouteDistanceProvider>());
        services.AddTransient<IWalkingRouteDistanceProvider>(provider => provider.GetRequiredService<GeoapifyRouteDistanceProvider>());

        return services;
    }
}
