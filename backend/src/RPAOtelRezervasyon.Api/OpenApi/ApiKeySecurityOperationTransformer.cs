using Microsoft.OpenApi;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Authorization;
using RPAOtelRezervasyon.Api.Auth;

namespace RPAOtelRezervasyon.Api.OpenApi;

/// <summary>
/// Korumalı OpenAPI operasyonlarına API anahtarı güvenlik şemasını ve gereksinimini ekler. Böylece
/// Swagger UI'daki "Authorize" düğmesi `X-Api-Key` başlığını gönderebilir ve belge, kimlik
/// doğrulama gereksinimini açıkça gösterir (0002 AC-3).
/// </summary>
public sealed class ApiKeySecurityOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (!endpointMetadata.OfType<IAuthorizeData>().Any())
        {
            return Task.CompletedTask;
        }

        var document = context.Document
            ?? throw new InvalidOperationException("OpenAPI transformer belgesi bulunamadı.");

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[ApiKeyDefaults.Scheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            Name = ApiKeyDefaults.HeaderName,
            In = ParameterLocation.Header,
            Description = $"API anahtarı; isteklerde {ApiKeyDefaults.HeaderName} başlığı ile gönderilir.",
        };

        var schemeReference = new OpenApiSecuritySchemeReference(ApiKeyDefaults.Scheme, document, null);
        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement { [schemeReference] = [] });

        return Task.CompletedTask;
    }
}
