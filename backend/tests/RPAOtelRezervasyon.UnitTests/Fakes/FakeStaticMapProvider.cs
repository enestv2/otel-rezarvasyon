using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.UnitTests.Fakes;

/// <summary>Test amaçlı coğrafi görsel sağlayıcısı; ağa çıkmaz, çağrıyı ve isteği kaydeder.</summary>
public sealed class FakeStaticMapProvider(string providerId = "fake") : IStaticMapProvider
{
    public string ProviderId { get; } = providerId;

    public int CallCount { get; private set; }

    public StaticMapRequest? LastRequest { get; private set; }

    public OutcomeStatus Status { get; set; } = OutcomeStatus.Found;

    public ReadOnlyMemory<byte> Image { get; set; } = new byte[] { 1, 2, 3, 4 };

    public string ContentType { get; set; } = "image/png";

    public string? Attribution { get; set; } = "Şematik görünüm (test)";

    public Task<StaticMapOutcome> RenderAsync(StaticMapRequest request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;

        return Task.FromResult(Status switch
        {
            OutcomeStatus.TransientError => StaticMapOutcome.TransientError(ProviderId),
            OutcomeStatus.ProviderError => StaticMapOutcome.ProviderError(ProviderId),
            OutcomeStatus.Found => StaticMapOutcome.Found(ProviderId, Image, ContentType, Attribution),
            _ => StaticMapOutcome.NotFound(ProviderId),
        });
    }
}