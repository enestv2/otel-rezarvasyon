using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace RPAOtelRezervasyon.Api.Errors;

/// <summary>
/// Tek hata deseni: RFC 7807 `ProblemDetails` (docs/conventions.md). Yanıtlara yalnızca korelasyon
/// kimliği eklenir; stack trace, dosya yolu veya sağlayıcı ham yanıtı sızdırılmaz (docs/security.md).
/// </summary>
public static class ProblemDetailsConfiguration
{
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);

        return services;
    }
}
