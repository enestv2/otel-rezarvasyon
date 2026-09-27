using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace RPAOtelRezervasyon.IntegrationTests.Support;

internal sealed class GeoapifyStubHttpMessageHandler(ConcurrentQueue<Uri> requestUris) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("Geoapify request URI is missing.");
        requestUris.Enqueue(uri);

        var payload = uri.AbsolutePath.EndsWith("/geocode/search", StringComparison.Ordinal)
            ? """{"results":[{"lat":39.9208,"lon":32.8541,"formatted":"Ankara"}]}"""
            : """{"features":[{"properties":{"distance":1200,"time":180},"geometry":{"type":"MultiLineString","coordinates":[[[32.8541,39.9208],[32.8600,39.9300]]]}}]}""";

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        });
    }
}
