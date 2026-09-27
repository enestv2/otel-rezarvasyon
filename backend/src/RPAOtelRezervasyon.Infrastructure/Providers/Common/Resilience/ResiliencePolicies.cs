using System.Threading.RateLimiting;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.RateLimiting;

namespace RPAOtelRezervasyon.Infrastructure.Providers.Common.Resilience;

/// <summary>
/// Sağlayıcı çağrıları için dayanıklılık stratejileri. Sıra mimari sözleşmedir
/// (docs/architecture.md): `rate limiter → total timeout → retry → circuit breaker → attempt timeout`.
/// Retry yalnızca idempotent GET içindir ve jitter'lıdır (docs/conventions.md).
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// Sağlayıcının hız sınırını uygulayan token bucket. Kuyruk sınırsızdır; fazla istek bekletilir,
    /// reddedilmez (devre kesici zaten hata yönetir).
    /// Örnek, DI'da tekil olarak kaydedilir ve uygulama kapanışında DI tarafından atılır; böylece
    /// HttpClient işleyici dönüşümlerinde (handler rotation) limiter yeniden oluşturulmaz.
    /// </summary>
    public static RateLimiter CreateProviderRateLimiter(double requestsPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestsPerSecond);

        var replenishmentPeriod = TimeSpan.FromSeconds(1d / requestsPerSecond);

        return new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 1,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = replenishmentPeriod,
            AutoReplenishment = true,
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    }

    /// <summary>Verilen limiter'ı Polly hız sınırı stratejisine bağlar.</summary>
    public static RateLimiterStrategyOptions CreateRateLimiterStrategy(RateLimiter limiter)
    {
        ArgumentNullException.ThrowIfNull(limiter);

        return new RateLimiterStrategyOptions
        {
            RateLimiter = arguments => limiter.AcquireAsync(1, arguments.Context.CancellationToken),
        };
    }

    /// <summary>Idempotent GET için sınırlı, üstel ve jitter'lı retry.</summary>
    public static HttpRetryStrategyOptions CreateRetry(int maxRetryAttempts)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetryAttempts);

        return new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = maxRetryAttempts,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
        };
    }

    /// <summary>Varsayılan devre kesici: örnekleme 30 sn, hata oranı 0,1, kesinti 5 sn.</summary>
    public static HttpCircuitBreakerStrategyOptions CreateCircuitBreaker() => new();
}
