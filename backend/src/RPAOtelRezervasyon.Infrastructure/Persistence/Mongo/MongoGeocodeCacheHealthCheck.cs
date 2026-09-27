using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RPAOtelRezervasyon.Infrastructure.Persistence.Mongo;

/// <summary>Reports MongoDB availability for the geocode cache without exposing connection details.</summary>
public sealed class MongoGeocodeCacheHealthCheck(IMongoDatabase database) : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            await database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                cancellationToken: timeout.Token).ConfigureAwait(false);

            return HealthCheckResult.Healthy("Mongo geocode cache is available.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is MongoException or OperationCanceledException or TimeoutException)
        {
            return HealthCheckResult.Degraded("Mongo geocode cache is unavailable.");
        }
    }
}
