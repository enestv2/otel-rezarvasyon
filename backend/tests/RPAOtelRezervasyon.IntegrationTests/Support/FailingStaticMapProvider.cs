using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.IntegrationTests.Support;

/// <summary>
/// Coğrafi görsel üretimini her zaman geçici hatayla bitiren sahte sağlayıcı. Uçtan uca akışta
/// "görsel üretilemedi" yolunu (R-8/AC-8) sınamak için kullanılır; PDF üretimi başarılı kalmalıdır.
/// </summary>
internal sealed class FailingStaticMapProvider : IStaticMapProvider
{
    public string ProviderId => "failing-static-map";

    public Task<StaticMapOutcome> RenderAsync(StaticMapRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(StaticMapOutcome.TransientError(ProviderId));
}