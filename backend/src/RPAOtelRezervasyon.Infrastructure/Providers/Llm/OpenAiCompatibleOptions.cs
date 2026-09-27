namespace RPAOtelRezervasyon.Infrastructure.Providers.Llm;

public sealed class OpenAiCompatibleOptions
{
    public const string SectionName = "Llm";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string Model { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 25;

    public int MaxOutputTokens { get; set; } = 350;

    public double Temperature { get; set; } = 0;

}
