using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RPAOtelRezervasyon.Infrastructure.Providers.Common.Options;

/// <summary>
/// Sağlayıcı seçeneklerini tutarlı biçimde bağlar ve doğrular: `IOptions&lt;T&gt;` +
/// `ValidateDataAnnotations()` + `ValidateOnStart()` (docs/conventions.md).
/// </summary>
public static class OptionsValidation
{
    public static OptionsBuilder<TOptions> AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        return services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
