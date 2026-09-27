using System.Net;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Providers.Geoapify;
using RPAOtelRezervasyon.ProviderTests.Support;

namespace RPAOtelRezervasyon.ProviderTests;

public sealed class GeoapifyProviderTests
{
    private static readonly GeoPoint Origin = new(39.9208, 32.8541);
    private static readonly GeoPoint Destination = new(41.0000, 29.0000);

    [Fact]
    public async Task GeocodeAsync_sends_structured_place_city_country_and_maps_coordinates()
    {
        const string payload = """{"results":[{"lat":39.9208,"lon":32.8541,"formatted":"Congresium, Ankara"}]}""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, payload));
        var provider = CreateGeocoder(handler);

        var outcome = await provider.GeocodeAsync("Congresium & Kongre", new GeocodingContext("Ankara", "Türkiye"), CancellationToken.None);

        Assert.Equal(OutcomeStatus.Found, outcome.Status);
        Assert.Equal("geoapify", outcome.ProviderId);
        Assert.Equal(Origin, outcome.Location);
        Assert.Equal("Congresium, Ankara", outcome.DisplayName);
        var uri = Assert.Single(handler.RequestUris);
        Assert.Equal("https://api.geoapify.test/v1/geocode/search", uri.GetLeftPart(UriPartial.Path));
        Assert.Contains("name=Congresium%20%26%20Kongre", uri.Query, StringComparison.Ordinal);
        Assert.Contains("city=Ankara", uri.Query, StringComparison.Ordinal);
        Assert.Contains("country=Türkiye", Uri.UnescapeDataString(uri.Query), StringComparison.Ordinal);
        Assert.Contains("apiKey=test-secret", uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeocodeAsync_when_results_empty_returns_not_found()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, "{\"results\":[]}"));
        var outcome = await CreateGeocoder(handler).GeocodeAsync("Missing", new GeocodingContext("Ankara", "Turkey"), CancellationToken.None);

        Assert.Equal(OutcomeStatus.NotFound, outcome.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, OutcomeStatus.ProviderError)]
    [InlineData(HttpStatusCode.ServiceUnavailable, OutcomeStatus.TransientError)]
    public async Task GeocodeAsync_maps_provider_http_errors(HttpStatusCode statusCode, OutcomeStatus expected)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode));

        var outcome = await CreateGeocoder(handler).GeocodeAsync(
            "Venue", new GeocodingContext("Ankara", "Turkey"), CancellationToken.None);

        Assert.Equal(expected, outcome.Status);
    }

    [Fact]
    public async Task GetRouteAsync_maps_route_metrics_and_multiline_geojson_path()
    {
        const string payload = """
            {"type":"FeatureCollection","features":[{"type":"Feature","properties":{"distance":1234.6,"time":95.4},"geometry":{"type":"MultiLineString","coordinates":[[[32.8541,39.9208],[32.9,40.0]],[[32.9,40.0],[29.0,41.0]]]}}]}
            """;
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, payload));
        var provider = CreateRouter(handler);

        var outcome = await provider.GetRouteAsync(Origin, Destination, CancellationToken.None);

        Assert.Equal(OutcomeStatus.Found, outcome.Status);
        Assert.NotNull(outcome.Metrics);
        Assert.Equal(1235, outcome.Metrics.DistanceMeters);
        Assert.Equal(95, outcome.Metrics.DurationSeconds);
        Assert.Equal(DistanceKind.Road, outcome.Metrics.Kind);
        Assert.Equal([Origin, new GeoPoint(40.0, 32.9), new GeoPoint(40.0, 32.9), Destination], outcome.Metrics.Path);
        var uri = Assert.Single(handler.RequestUris);
        Assert.Equal("/v1/routing", uri.AbsolutePath);
        Assert.Contains("waypoints=39.920800%2C32.854100%7C41.000000%2C29.000000", uri.Query, StringComparison.Ordinal);
        Assert.Contains("mode=drive", uri.Query, StringComparison.Ordinal);
        Assert.Contains("format=geojson", uri.Query, StringComparison.Ordinal);
        Assert.Contains("apiKey=test-secret", uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetRouteAsync_preserves_disconnected_multiline_components_without_joining_them()
    {
        const string payload = """
            {"type":"FeatureCollection","features":[{"type":"Feature","properties":{"distance":1234,"time":95},"geometry":{"type":"MultiLineString","coordinates":[[[32.8541,39.9208],[32.8550,39.9210]],[[32.8700,39.9400],[32.8710,39.9410]]]}}]}
            """;
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, payload));
        var outcome = await CreateRouter(handler).GetRouteAsync(Origin, Destination, CancellationToken.None);

        Assert.Equal(OutcomeStatus.Found, outcome.Status);
        var segmentsProperty = typeof(RouteMetrics).GetProperty("PathSegments");
        Assert.NotNull(segmentsProperty);
        var segments = Assert.IsAssignableFrom<IEnumerable<IEnumerable<GeoPoint>>>(segmentsProperty!.GetValue(outcome.Metrics));
        Assert.Equal(
            new[]
            {
                new[] { Origin, new GeoPoint(39.9210, 32.8550) },
                new[] { new GeoPoint(39.9400, 32.8700), new GeoPoint(39.9410, 32.8710) },
            },
            segments.Select(segment => segment.ToArray()).ToArray());
    }

    [Fact]
    public async Task GetWalkingRouteAsync_requests_walk_mode_and_returns_metrics_and_geometry()
    {
        const string payload = """
            {"type":"FeatureCollection","features":[{"type":"Feature","properties":{"distance":765.4,"time":540.6},"geometry":{"type":"MultiLineString","coordinates":[[[32.8541,39.9208],[29.0,41.0]]]}}]}
            """;
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, payload));
        var provider = CreateRouter(handler);

        var outcome = await provider.GetWalkingRouteAsync(Origin, Destination, CancellationToken.None);

        Assert.Equal(OutcomeStatus.Found, outcome.Status);
        Assert.Equal(765, outcome.Metrics?.DistanceMeters);
        Assert.Equal(541, outcome.Metrics?.DurationSeconds);
        Assert.Equal([Origin, Destination], outcome.Metrics?.Path);
        Assert.Contains("mode=walk", Assert.Single(handler.RequestUris).Query, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, OutcomeStatus.ProviderError)]
    [InlineData(HttpStatusCode.TooManyRequests, OutcomeStatus.TransientError)]
    public async Task GetRouteAsync_maps_provider_http_errors(HttpStatusCode statusCode, OutcomeStatus expected)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode));
        var outcome = await CreateRouter(handler).GetRouteAsync(Origin, Destination, CancellationToken.None);

        Assert.Equal(expected, outcome.Status);
    }

    [Fact]
    public async Task Geoapify_adapters_do_not_log_api_key_or_searched_place()
    {
        var logger = new RecordingLogger<GeoapifyGeocodingProvider>();
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}"));
        var provider = new GeoapifyGeocodingProvider(
            CreateClient(handler),
            Microsoft.Extensions.Options.Options.Create(CreateProviderOptions()),
            logger);

        await provider.GeocodeAsync("Confidential Venue", new GeocodingContext("Private City", "Private Country"), CancellationToken.None);

        var logs = string.Join(" ", logger.Messages);
        Assert.DoesNotContain("test-secret", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("Confidential Venue", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("Private City", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("Private Country", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("apiKey", logs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Geoapify_typed_http_client_does_not_expose_api_key_in_logs_or_activities()
    {
        var logs = new RecordingLoggerProvider();
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);

        using var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        var endpoint = (IPEndPoint)server.LocalEndpoint;
        var serverTask = Task.Run(async () =>
        {
            using var connection = await server.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync() ?? string.Empty;
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) { }
            const string payload = "{\"results\":[]}";
            var response = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Encoding.ASCII.GetByteCount(payload)}\r\nConnection: close\r\n\r\n{payload}");
            await stream.WriteAsync(response);
            return requestLine;
        });

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Geoapify:ApiKey"] = "test-secret",
            ["Geoapify:BaseUrl"] = $"http://127.0.0.1:{endpoint.Port}/",
            ["Geoapify:RetryMaxAttempts"] = "1",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(logs).SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace));
        services.AddGeoapifyProviders(configuration);
        await using var serviceProvider = services.BuildServiceProvider();

        await serviceProvider.GetRequiredService<GeoapifyGeocodingProvider>().GeocodeAsync(
            "Venue", new GeocodingContext("Ankara", "Turkey"), CancellationToken.None);

        Assert.Contains("apiKey=test-secret", await serverTask, StringComparison.Ordinal);
        var logText = string.Join(" ", logs.Messages);
        var activityText = string.Join(" ", activities.SelectMany(activity => activity.Tags.Select(tag => $"{tag.Key}={tag.Value}")));
        Assert.NotEmpty(logs.Messages);
        Assert.Contains(activities, activity =>
            activity.Source.Name.Contains("Http", StringComparison.OrdinalIgnoreCase)
            && activity.Tags.Any(tag => tag.Value?.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase) == true));
        Assert.DoesNotContain("test-secret", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("test-secret", activityText, StringComparison.Ordinal);
    }

    private sealed class RecordingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private sealed class RecordingLoggerProvider : Microsoft.Extensions.Logging.ILoggerProvider
    {
        public List<string> Messages { get; } = [];
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages);
        public void Dispose() { }

        private sealed class RecordingLogger(List<string> messages) : Microsoft.Extensions.Logging.ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
                TState state, Exception? exception, Func<TState, Exception?, string> formatter) => messages.Add(formatter(state, exception));
        }
    }
    [Fact]
    public void AddGeoapifyProviders_defaults_to_geoapify_and_requires_a_secret()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGeoapifyProviders(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GeoapifyOptions>>().Value);
    }

    [Fact]
    public async Task AddGeoapifyProviders_host_start_fails_when_default_geoapify_secret_is_missing()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddGeoapifyProviders(configuration);
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains("Geoapify:ApiKey", exception.Message, StringComparison.Ordinal);
    }

    private static GeoapifyGeocodingProvider CreateGeocoder(StubHttpMessageHandler handler) =>
        new(CreateClient(handler), Microsoft.Extensions.Options.Options.Create(CreateProviderOptions()), NullLogger<GeoapifyGeocodingProvider>.Instance);

    private static GeoapifyRouteDistanceProvider CreateRouter(StubHttpMessageHandler handler) =>
        new(CreateClient(handler), Microsoft.Extensions.Options.Options.Create(CreateProviderOptions()), NullLogger<GeoapifyRouteDistanceProvider>.Instance);

    private static HttpClient CreateClient(StubHttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("https://api.geoapify.test/"),
    };

    private static GeoapifyOptions CreateProviderOptions() => new() { ApiKey = "test-secret" };

}
