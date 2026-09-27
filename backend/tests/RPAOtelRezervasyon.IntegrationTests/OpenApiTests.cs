using System.Net;
using System.Text.Json;
using RPAOtelRezervasyon.Api.Auth;
using RPAOtelRezervasyon.IntegrationTests.Support;

namespace RPAOtelRezervasyon.IntegrationTests;

public sealed class OpenApiTests
{
    [Fact]
    public async Task Development_exposes_openapi_ui_and_authenticated_operation_metadata()
    {
        await using var factory = new ApiTestFactory("Development");
        using var client = factory.CreateClient();

        using var documentResponse = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        using var document = JsonDocument.Parse(await documentResponse.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var operation = root.GetProperty("paths")
            .GetProperty("/api/recommendations")
            .GetProperty("post");

        Assert.Equal("ApiKey", operation.GetProperty("security")[0].EnumerateObject().First().Name);
        var apiKeyScheme = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("ApiKey");
        Assert.Equal("apiKey", apiKeyScheme.GetProperty("type").GetString());
        Assert.Equal("header", apiKeyScheme.GetProperty("in").GetString());
        Assert.Equal(ApiKeyDefaults.HeaderName, apiKeyScheme.GetProperty("name").GetString());
        foreach (var status in new[] { "200", "400", "401", "422" })
        {
            var response = operation.GetProperty("responses").GetProperty(status);
            var content = response.GetProperty("content");
            Assert.NotEmpty(content.EnumerateObject());
            Assert.Contains(content.EnumerateObject(), mediaType => mediaType.Value.TryGetProperty("schema", out _));
        }

        Assert.Contains(operation.GetProperty("responses").GetProperty("200")
            .GetProperty("content").EnumerateObject().Select(mediaType => mediaType.Value.GetProperty("schema")
                .GetProperty("$ref").GetString()), schemaReference => schemaReference?.EndsWith("RecommendationResponseDto", StringComparison.Ordinal) == true);
        Assert.Contains(operation.GetProperty("responses").GetProperty("400")
            .GetProperty("content").EnumerateObject().Select(mediaType => mediaType.Value.GetProperty("schema")
                .GetProperty("$ref").GetString()), schemaReference => schemaReference?.EndsWith("ValidationProblemDetails", StringComparison.Ordinal) == true);
        foreach (var status in new[] { "401", "422" })
        {
            Assert.Contains(operation.GetProperty("responses").GetProperty(status)
                .GetProperty("content").EnumerateObject().Select(mediaType => mediaType.Value.GetProperty("schema")
                    .GetProperty("$ref").GetString()), schemaReference => schemaReference?.EndsWith("/ProblemDetails", StringComparison.Ordinal) == true);
        }

        using var uiResponse = await client.GetAsync("/swagger");
        Assert.Equal(HttpStatusCode.OK, uiResponse.StatusCode);
        using var initializerResponse = await client.GetAsync("/swagger/index.js");
        Assert.Equal(HttpStatusCode.OK, initializerResponse.StatusCode);
        var initializer = await initializerResponse.Content.ReadAsStringAsync();
        Assert.Contains("SwaggerUIBundle", initializer, StringComparison.Ordinal);
        Assert.Contains("/openapi/v1.json", initializer, StringComparison.Ordinal);

        using var protectedResponse = await client.PostAsync("/api/recommendations", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
        Assert.Equal("application/problem+json", protectedResponse.Content.Headers.ContentType?.MediaType);
        using var challengeDocument = JsonDocument.Parse(await protectedResponse.Content.ReadAsStringAsync());
        Assert.Equal(401, challengeDocument.RootElement.GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Non_development_environments_do_not_expose_openapi_or_swagger_ui(string environment)
    {
        await using var factory = new ApiTestFactory(environment);
        using var client = factory.CreateClient();

        using var documentResponse = await client.GetAsync("/openapi/v1.json");
        using var uiResponse = await client.GetAsync("/swagger");

        Assert.Equal(HttpStatusCode.NotFound, documentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, uiResponse.StatusCode);
    }
}
