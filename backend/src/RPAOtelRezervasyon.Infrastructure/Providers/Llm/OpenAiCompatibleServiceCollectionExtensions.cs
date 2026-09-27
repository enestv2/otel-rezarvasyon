using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPAOtelRezervasyon.Domain.Abstractions;

namespace RPAOtelRezervasyon.Infrastructure.Providers.Llm;

public static class OpenAiCompatibleServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAiCompatibleRecommendationExplanation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<OpenAiCompatibleOptions>()
            .Bind(configuration.GetSection(OpenAiCompatibleOptions.SectionName))
            .Validate(options => !options.Enabled || Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp),
                "Llm:BaseUrl mutlak HTTPS URL olmalı (yerel loopback HTTP kabul edilir).")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Model), "Llm:Model zorunludur.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.ApiKey), "Llm:ApiKey zorunludur.")
            .Validate(options => options.TimeoutSeconds is >= 1 and <= 120, "Llm:TimeoutSeconds 1-120 arasında olmalıdır.")
            .Validate(options => options.MaxOutputTokens is >= 32 and <= 2000, "Llm:MaxOutputTokens 32-2000 arasında olmalıdır.")
            .Validate(options => options.Temperature is >= 0 and <= 1, "Llm:Temperature 0-1 arasında olmalıdır.")
            .ValidateOnStart();

        services.AddHttpClient<OpenAiCompatibleRecommendationExplanationProvider>((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<OpenAiCompatibleOptions>>().Value;
                if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
                {
                    client.BaseAddress = baseUri;
                }

                client.Timeout = Timeout.InfiniteTimeSpan;
            });
        services.AddTransient<IRecommendationExplanationProvider>(provider =>
            provider.GetRequiredService<OpenAiCompatibleRecommendationExplanationProvider>());
        services.AddLogging(logging =>
            logging.AddFilter("System.Net.Http.HttpClient.OpenAiCompatibleRecommendationExplanationProvider", LogLevel.None));

        return services;
    }
}
