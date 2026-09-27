using System.Net;
using System.Text;
using System.Collections.Concurrent;

namespace RPAOtelRezervasyon.ProviderTests.Support;

/// <summary>
/// Ağa çıkmadan sağlayıcı yanıtlarını taklit eden birincil işleyici. Gönderilen istek URI'leri ve
/// `User-Agent` başlıkları test için kaydedilir.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private const string UserAgentHeader = "User-Agent";

    private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _responder;
    private readonly ConcurrentQueue<Uri> _requestUris = new();
    private readonly ConcurrentQueue<string?> _userAgents = new();
    private readonly ConcurrentQueue<HttpMethod> _requestMethods = new();
    private readonly ConcurrentQueue<string?> _requestBodies = new();

    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder) =>
        _responder = responder;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : this((request, _) => responder(request))
    {
    }

    public IReadOnlyList<Uri> RequestUris => _requestUris.ToArray();

    public IReadOnlyList<string?> UserAgents => _userAgents.ToArray();

    public IReadOnlyList<HttpMethod> RequestMethods => _requestMethods.ToArray();

    public IReadOnlyList<string?> RequestBodies => _requestBodies.ToArray();

    public static HttpResponseMessage Json(HttpStatusCode statusCode, string payload) =>
        new(statusCode) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _requestUris.Enqueue(request.RequestUri!);
        _requestMethods.Enqueue(request.Method);
        _requestBodies.Enqueue(request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());
        _userAgents.Enqueue(request.Headers.TryGetValues(UserAgentHeader, out var values)
            ? string.Join(", ", values)
            : null);

        return Task.FromResult(_responder(request, cancellationToken));
    }
}
