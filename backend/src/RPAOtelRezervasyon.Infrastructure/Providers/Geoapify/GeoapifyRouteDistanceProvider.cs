using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Providers.Common.Http;

namespace RPAOtelRezervasyon.Infrastructure.Providers.Geoapify;

public sealed class GeoapifyRouteDistanceProvider(
    HttpClient httpClient,
    IOptions<GeoapifyOptions> options,
    ILogger<GeoapifyRouteDistanceProvider> logger) : IRouteDistanceProvider, IWalkingRouteDistanceProvider
{
    public const string Id = "geoapify";
    private const int MaxPathPoints = 512;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ProviderId => Id;

    public Task<RouteOutcome> GetRouteAsync(GeoPoint origin, GeoPoint destination, CancellationToken cancellationToken) =>
        GetRouteAsync(origin, destination, options.Value.RoutingMode, cancellationToken);

    public Task<RouteOutcome> GetWalkingRouteAsync(GeoPoint origin, GeoPoint destination, CancellationToken cancellationToken) =>
        GetRouteAsync(origin, destination, "walk", cancellationToken);

    private async Task<RouteOutcome> GetRouteAsync(GeoPoint origin, GeoPoint destination, string mode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(destination);

        var waypoints = string.Create(CultureInfo.InvariantCulture,
            $"{origin.Latitude:F6},{origin.Longitude:F6}|{destination.Latitude:F6},{destination.Longitude:F6}");
        var uri = $"v1/routing?waypoints={Uri.EscapeDataString(waypoints)}&mode={Uri.EscapeDataString(mode)}&units=metric&format=geojson&apiKey={Uri.EscapeDataString(options.Value.ApiKey)}";

        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Geoapify routing yanıtı başarısız: {StatusCode}.", (int)response.StatusCode);
                return ProviderHttpStatus.IsTransient(response.StatusCode)
                    ? RouteOutcome.TransientError()
                    : RouteOutcome.ProviderError();
            }

            var payload = await DeserializeAsync(response, cancellationToken).ConfigureAwait(false);
            var route = payload?.Features?.FirstOrDefault();
            if (route?.Properties is not { } properties || !TryCreateMetrics(properties, route.Geometry, out var metrics))
            {
                return RouteOutcome.NotFound();
            }

            return RouteOutcome.Found(metrics);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Geoapify routing zaman aşımına uğradı.");
            return RouteOutcome.TransientError();
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("Geoapify routing isteği başarısız oldu.");
            return RouteOutcome.TransientError();
        }
        catch (JsonException)
        {
            logger.LogWarning("Geoapify routing yanıtı beklenen şemaya uymuyor.");
            return RouteOutcome.TransientError();
        }
    }

    private static async Task<GeoapifyRouteResponse?> DeserializeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<GeoapifyRouteResponse>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryCreateMetrics(
        GeoapifyRouteProperties properties,
        GeoapifyRouteGeometry? geometry,
        out RouteMetrics metrics)
    {
        metrics = null!;
        if (!double.IsFinite(properties.Distance) || !double.IsFinite(properties.Time)
            || properties.Distance < 0 || properties.Time < 0
            || properties.Distance > int.MaxValue || properties.Time > int.MaxValue)
        {
            return false;
        }

        var segments = ParsePathSegments(geometry);
        metrics = new RouteMetrics(
            (int)Math.Round(properties.Distance, MidpointRounding.AwayFromZero),
            (int)Math.Round(properties.Time, MidpointRounding.AwayFromZero),
            DistanceKind.Road,
            segments.SelectMany(segment => segment).ToArray(),
            segments);
        return true;
    }

    private static IReadOnlyList<IReadOnlyList<GeoPoint>> ParsePathSegments(GeoapifyRouteGeometry? geometry)
    {
        if (geometry is not { Type: "MultiLineString", MultiLineCoordinates: { Count: > 0 } lines })
        {
            return [];
        }

        var segments = new List<IReadOnlyList<GeoPoint>>();
        foreach (var line in lines)
        {
            var points = new List<GeoPoint>();
            foreach (var coordinate in line)
            {
                if (coordinate is not { Length: >= 2 })
                {
                    continue;
                }

                var longitude = coordinate[0];
                var latitude = coordinate[1];
                if (double.IsFinite(latitude) && double.IsFinite(longitude)
                    && latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180)
                {
                    points.Add(new GeoPoint(latitude, longitude));
                }
            }

            if (points.Count >= 2)
            {
                segments.Add(points);
            }
        }

        if (segments.Count == 0)
        {
            return [];
        }

        var pointCount = segments.Sum(segment => segment.Count);
        if (pointCount <= MaxPathPoints)
        {
            return segments;
        }

        var capped = new List<IReadOnlyList<GeoPoint>>();
        var remaining = MaxPathPoints;
        foreach (var segment in segments)
        {
            if (remaining < 2) break;
            var take = Math.Min(segment.Count, remaining);
            capped.Add(segment.Take(take).ToArray());
            remaining -= take;
        }
        return capped;
    }
}
