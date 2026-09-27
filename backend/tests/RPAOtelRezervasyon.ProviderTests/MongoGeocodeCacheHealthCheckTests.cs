using System.Reflection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using RPAOtelRezervasyon.Infrastructure.Persistence.Mongo;

namespace RPAOtelRezervasyon.ProviderTests;

public sealed class MongoGeocodeCacheHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_returns_healthy_when_ping_succeeds()
    {
        var database = CreateDatabase();
        var healthCheck = new MongoGeocodeCacheHealthCheck(database);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("available", result.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_degraded_without_details_when_ping_fails()
    {
        var database = CreateDatabase(new TimeoutException("connection string with secret"));
        var healthCheck = new MongoGeocodeCacheHealthCheck(database);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.DoesNotContain("secret", result.Description, StringComparison.Ordinal);
    }

    private static IMongoDatabase CreateDatabase(Exception? exception = null)
    {
        var database = DispatchProxy.Create<IMongoDatabase, MongoDatabaseProxy>();
        ((MongoDatabaseProxy)(object)database).Exception = exception;
        return database;
    }

    public class MongoDatabaseProxy : DispatchProxy
    {
        public Exception? Exception { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "RunCommandAsync")
            {
                if (Exception is not null)
                {
                    throw Exception;
                }

                var resultType = targetMethod.ReturnType.GenericTypeArguments[0];
                var result = Activator.CreateInstance(resultType);
                var fromResult = typeof(Task).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Single(method => method.Name == nameof(Task.FromResult) && method.IsGenericMethodDefinition)
                    .MakeGenericMethod(resultType);
                return fromResult.Invoke(null, [result]);
            }

            throw new NotSupportedException($"Unexpected Mongo database call: {targetMethod?.Name}");
        }
    }
}
