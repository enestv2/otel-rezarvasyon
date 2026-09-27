using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Providers.Llm;

namespace RPAOtelRezervasyon.ProviderTests;

public sealed class OpenAiCompatibleRecommendationExplanationProviderTests
{
    [Fact]
    public async Task GenerateAsync_reads_incremental_sse_and_sends_all_recommendation_results()
    {
        var chunks = new[]
        {
            "{\"options\":[{\"strategy\":\"BudgetPriority\",\"reasonCodes\":[\"lowest-price\"]},",
            "{\"strategy\":\"TransportPriority\",\"reasonCodes\":[\"shortest-road-time\"]},",
            "{\"strategy\":\"Balanced\",\"reasonCodes\":[\"within-ten-percent-budget-band\"]}]}",
        };
        var handler = new RecordingHandler(Sse(chunks));
        var provider = CreateProvider(handler);

        var outcome = await provider.GenerateAsync(Request(), CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(3, outcome.Options.Count);
        Assert.Contains("shortest-road-time", outcome.Options.Single(option => option.Strategy == RecommendationStrategy.TransportPriority).ReasonCodes);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://llm.example.test/v1/chat/completions", handler.RequestUri!.ToString());
        Assert.Equal("test-only-key", handler.AuthorizationParameter);
        using var requestJson = JsonDocument.Parse(handler.Body!);
        Assert.True(requestJson.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal("demo-model", requestJson.RootElement.GetProperty("model").GetString());
        var userContent = requestJson.RootElement.GetProperty("messages")[1].GetProperty("content").GetString();
        Assert.Contains("Otel A", userContent);
        Assert.Contains("Otel B", userContent);
        Assert.Contains("Konum Yok", userContent);
        Assert.Contains("41.1", userContent);
        Assert.DoesNotContain("test-only-key", handler.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("S12345", userContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_rejects_incomplete_stream_without_done_marker()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Sse(["{\"options\":[]}"]), Encoding.UTF8, "text/event-stream"),
        };
        var provider = CreateProvider(new RecordingHandler(response));

        var outcome = await provider.GenerateAsync(Request(), CancellationToken.None);

        Assert.False(outcome.IsSuccess);
    }

    [Fact]
    public async Task GenerateAsync_returns_failure_for_non_success_provider_response()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        var provider = CreateProvider(new RecordingHandler(response));

        var outcome = await provider.GenerateAsync(Request(), CancellationToken.None);

        Assert.False(outcome.IsSuccess);
    }

    private static OpenAiCompatibleRecommendationExplanationProvider CreateProvider(HttpMessageHandler handler) =>
        new(new HttpClient(handler), Options.Create(new OpenAiCompatibleOptions
        {
            Enabled = true,
            BaseUrl = "https://llm.example.test/v1",
            Model = "demo-model",
            ApiKey = "test-only-key",
            TimeoutSeconds = 5,
        }));

    private static RecommendationExplanationRequest Request() => new(
        "Etkinlik Alanı",
        [
            new RecommendationExplanationHotel(1, "Otel A", 1200m, "TRY", 1000, 600, DistanceKind.Road, 41.1, 29.1, true, 850, 540),
            new RecommendationExplanationHotel(2, "Otel B", 900m, "TRY", 5000, 1500, DistanceKind.Road, 41.2, 29.2, false, null, null),
        ],
        ["Konum Yok"],
        [
            new RecommendationExplanationPolicyOption(RecommendationStrategy.BudgetPriority, "Otel B", ["lowest-price"]),
            new RecommendationExplanationPolicyOption(RecommendationStrategy.TransportPriority, "Otel A", ["shortest-road-time"]),
            new RecommendationExplanationPolicyOption(RecommendationStrategy.Balanced, "Otel A", ["within-ten-percent-budget-band"]),
        ]);

    private static string Sse(IEnumerable<string> chunks) =>
        string.Join("\n\n", chunks.Select(chunk => $"data: {JsonSerializer.Serialize(new { choices = new[] { new { delta = new { content = chunk } } } })}"))
        + "\n\ndata: [DONE]\n\n";

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public RecordingHandler(string sse)
            : this(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sse, Encoding.UTF8, "text/event-stream"),
            })
        {
        }

        public RecordingHandler(HttpResponseMessage response) => _response = response;

        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return _response;
        }
    }
}
