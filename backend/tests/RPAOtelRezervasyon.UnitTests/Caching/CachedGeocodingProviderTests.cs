using FluentAssertions;
using RPAOtelRezervasyon.Application.Caching;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.UnitTests.Fakes;

namespace RPAOtelRezervasyon.UnitTests.Caching;

public class CachedGeocodingProviderTests
{
    [Fact]
    public async Task GeocodeAsync_does_not_call_the_provider_on_a_cache_hit()
    {
        var inner = new FakeGeocodingProvider().With("Grand Hotel", 41.0, 29.0);
        var cache = new InMemoryGeocodeCache();
        var provider = new CachedGeocodingProvider(inner, cache);

        var first = await provider.GeocodeAsync("Grand Hotel", CancellationToken.None);
        var second = await provider.GeocodeAsync("Grand Hotel", CancellationToken.None);

        first.Status.Should().Be(OutcomeStatus.Found);
        second.Status.Should().Be(OutcomeStatus.Found);
        inner.CallCount.Should().Be(1);
        cache.StoreCount.Should().Be(1);
    }

    [Fact]
    public async Task GeocodeAsync_treats_case_and_whitespace_variations_as_the_same_entry()
    {
        var inner = new FakeGeocodingProvider().With("Grand Hotel", 41.0, 29.0);
        var cache = new InMemoryGeocodeCache();
        var provider = new CachedGeocodingProvider(inner, cache);

        await provider.GeocodeAsync("Grand Hotel", CancellationToken.None);
        var second = await provider.GeocodeAsync("   grand    hotel  ", CancellationToken.None);

        second.Status.Should().Be(OutcomeStatus.Found);
        inner.CallCount.Should().Be(1);
        cache.FindCount.Should().Be(2);
    }

    [Fact]
    public async Task GeocodeAsync_does_not_store_failed_resolutions()
    {
        var inner = new FakeGeocodingProvider { MissingStatus = OutcomeStatus.NotFound };
        var cache = new InMemoryGeocodeCache();
        var provider = new CachedGeocodingProvider(inner, cache);

        await provider.GeocodeAsync("Bilinmeyen Yer", CancellationToken.None);
        await provider.GeocodeAsync("Bilinmeyen Yer", CancellationToken.None);

        inner.CallCount.Should().Be(2);
        cache.StoreCount.Should().Be(0);
    }

    [Fact]
    public async Task GeocodeAsync_includes_city_and_country_in_the_cache_identity()
    {
        var inner = new FakeGeocodingProvider().With("Grand Hotel", 39.9, 32.8);
        var provider = new CachedGeocodingProvider(inner, new InMemoryGeocodeCache());
        var ankara = new GeocodingContext("Ankara", "Turkey");

        await provider.GeocodeAsync("Grand Hotel", ankara, CancellationToken.None);
        await provider.GeocodeAsync("Grand Hotel", new GeocodingContext("Istanbul", "Turkey"), CancellationToken.None);
        await provider.GeocodeAsync("Grand Hotel", ankara, CancellationToken.None);

        inner.CallCount.Should().Be(2);
    }
}
