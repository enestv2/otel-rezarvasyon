using System.Net;

namespace RPAOtelRezervasyon.Infrastructure.Providers.Common.Http;

/// <summary>
/// Dış sağlayıcı HTTP durum kodlarını beklenen sonuç durumuna eşler (docs/architecture.md).
/// HTTP durum kodu çekirdeğe sızmaz; karar adaptör sınırında verilir.
/// </summary>
internal static class ProviderHttpStatus
{
    /// <summary>Geçici hata sayılan durumlar: yeniden denenebilir (429/408/5xx).</summary>
    public static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
        || (int)statusCode >= 500;
}
