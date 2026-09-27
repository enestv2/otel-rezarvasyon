using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Domain.Abstractions;

/// <summary>
/// Belge için coğrafi görsel üretir (R-4). Sağlayıcı değişebilir (ADR-0006); çekirdek yalnızca bu
/// arayüzü bilir ve hatayı sonuç tipiyle bekler.
/// </summary>
public interface IStaticMapProvider
{
    string ProviderId { get; }

    Task<StaticMapOutcome> RenderAsync(StaticMapRequest request, CancellationToken cancellationToken);
}