using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RPAOtelRezervasyon.Api.Auth;
using RPAOtelRezervasyon.Application.Caching;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Infrastructure.Persistence.Mongo;
using RPAOtelRezervasyon.Infrastructure.Providers.Geoapify;
using RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;

namespace RPAOtelRezervasyon.IntegrationTests.Support;

/// <summary>
/// Uçtan uca test host'u: gerçek uygulama boru hattını kurar, ancak dış sağlayıcıları ve kalıcı
/// önbelleği sahtelerle değiştirir. Böylece HTTP sözleşmesi, kimlik doğrulama, doğrulama ve durum
/// eşlemesi gerçek kodla sınanır; ağa çıkılmaz ve MongoDB gerekmez.
/// </summary>
internal sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "test-api-key";

    private readonly string environmentName;

    public ApiTestFactory(string environmentName = "Testing") => this.environmentName = environmentName;

    public FakeGeocodingProvider Geocoding { get; } = new();

    public FakeRouteDistanceProvider Routing { get; } = new();

    public FakeWalkingRouteDistanceProvider WalkingRouting { get; } = new();

    public InMemoryGeocodeCache Cache { get; } = new();

    public CapturingLoggerProvider LogCapture { get; } = new();

    public System.Collections.Concurrent.ConcurrentQueue<Uri> GeoapifyRequestUris { get; } = new();

    public bool ReplaceRecommendationProviders { get; set; } = true;

    public Func<HttpMessageHandler>? GeoapifyHttpHandlerFactory { get; set; }


    /// <summary>Verilirse gerçek şematik harita sağlayıcısı yerine bu sağlayıcı kullanılır (0006 AC-8).</summary>
    public IStaticMapProvider? StaticMapOverride { get; set; }

    public Func<HttpMessageHandler>? StaticMapHttpHandlerFactory { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);

        // Windows Event Log sağlayıcısı başsız/CI ortamlarında yazma izni olmadığından
        // yalnızca uyarı seviyesindeki bir log'da bile test host'unu düşürür. Sağlayıcıları
        // temizlemek testlerin HTTP sözleşmesi doğrulamalarını etkilemez.
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(LogCapture);
        });

        builder.ConfigureServices(services =>
        {
            services.Configure<ApiKeyOptions>(options => options.ApiKey = ApiKey);
            services.Configure<GeoapifyOptions>(options =>
            {
                options.ApiKey = "integration-geoapify-key";
                options.BaseUrl = "https://api.geoapify.test";
                options.RetryMaxAttempts = 1;
            });
            services.Configure<MongoGeocodeCacheOptions>(options =>
                options.ConnectionString = "mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=500");

            // Gerçek Mongo bağlantısı denemesin diye indeks kurucu hosted servisi kaldır.
            var indexInitializer = services.FirstOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(MongoIndexInitializer));
            if (indexInitializer is not null)
            {
                services.Remove(indexInitializer);
            }

            services.RemoveAll<IGeocodeCache>();
            services.AddSingleton<IGeocodeCache>(Cache);
            if (ReplaceRecommendationProviders)
            {
                services.RemoveAll<IGeocodingProvider>();
                services.RemoveAll<IRouteDistanceProvider>();
                services.RemoveAll<IWalkingRouteDistanceProvider>();
            }

            if (StaticMapOverride is not null)
            {
                services.RemoveAll<IStaticMapProvider>();
                services.AddSingleton<IStaticMapProvider>(StaticMapOverride);
            }

            if (StaticMapHttpHandlerFactory is not null)
            {
                services.Configure<StaticMapOptions>(options =>
                {
                    options.BaseUrl = "https://maps.example.test/v1/staticmap";
                    options.ApiKey = "integration-map-key";
                    options.RetryMaxAttempts = 1;
                });
                services.AddHttpClient<HostedStaticMapProvider>()
                    .ConfigurePrimaryHttpMessageHandler(() => StaticMapHttpHandlerFactory());
            }

            if (GeoapifyHttpHandlerFactory is not null)
            {
                services.AddHttpClient<GeoapifyGeocodingProvider>()
                    .ConfigurePrimaryHttpMessageHandler(() => GeoapifyHttpHandlerFactory());
                services.AddHttpClient<GeoapifyRouteDistanceProvider>()
                    .ConfigurePrimaryHttpMessageHandler(() => GeoapifyHttpHandlerFactory());
            }

            if (ReplaceRecommendationProviders)
            {
                services.AddSingleton<IGeocodingProvider>(serviceProvider => new CachedGeocodingProvider(
                    Geocoding,
                    serviceProvider.GetRequiredService<IGeocodeCache>()));
                services.AddSingleton<IRouteDistanceProvider>(Routing);
                services.AddSingleton<IWalkingRouteDistanceProvider>(WalkingRouting);
            }
        });
    }
}
