using System.Reflection;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Persistence.Mongo;

namespace RPAOtelRezervasyon.ProviderTests;

public sealed class MongoGeocodeCacheLoggingTests
{
    [Fact]
    public async Task StoreAsync_logs_success_at_information_level_without_location_data()
    {
        var collection = DispatchProxy.Create<IMongoCollection<GeocodeCacheDocument>, MongoCollectionProxy>();
        var capture = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(capture));
        var cache = new MongoGeocodeCache(collection, loggerFactory.CreateLogger<MongoGeocodeCache>());

        await cache.StoreAsync(
            "geoapify",
            "confidential venue",
            new GeoPoint(39.9208, 32.8541),
            "Confidential Venue, Ankara",
            CancellationToken.None);

        var message = Assert.Single(capture.Messages);
        Assert.Equal(LogLevel.Information, message.Level);
        Assert.Contains("Geocode cache upsert completed", message.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("confidential venue", message.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Confidential Venue, Ankara", message.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("39.9208", message.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("32.8541", message.Text, StringComparison.Ordinal);
    }

    public class MongoCollectionProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != "ReplaceOneAsync")
            {
                throw new NotSupportedException($"Unexpected Mongo collection call: {targetMethod?.Name}");
            }

            var resultType = targetMethod.ReturnType.GenericTypeArguments[0];
            var fromResult = typeof(Task).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == nameof(Task.FromResult) && method.IsGenericMethodDefinition)
                .MakeGenericMethod(resultType);
            return fromResult.Invoke(null, [null]);
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(LogLevel Level, string Text)> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);
        public void Dispose() { }

        private sealed class CapturingLogger(ConcurrentQueue<(LogLevel Level, string Text)> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => messages.Enqueue((logLevel, formatter(state, exception)));
        }
    }
}
