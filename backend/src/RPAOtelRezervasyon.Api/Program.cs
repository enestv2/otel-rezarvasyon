using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RPAOtelRezervasyon.Api.OpenApi;
using RPAOtelRezervasyon.Api.Auth;
using RPAOtelRezervasyon.Api.Errors;
using RPAOtelRezervasyon.Application.Modules.Delivery;
using RPAOtelRezervasyon.Application.Modules.DistanceRouting;
using RPAOtelRezervasyon.Application.Modules.HotelGeocoding;
using RPAOtelRezervasyon.Application.Modules.Ranking;
using RPAOtelRezervasyon.Application.Modules.Reporting;
using RPAOtelRezervasyon.Application.Modules.VenueGeocoding;
using RPAOtelRezervasyon.Application.Orchestration;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Infrastructure.Persistence.Mongo;
using RPAOtelRezervasyon.Infrastructure.Providers.HealthChecks;
using RPAOtelRezervasyon.Infrastructure.Providers.Geoapify;
using RPAOtelRezervasyon.Infrastructure.Providers.Llm;
using RPAOtelRezervasyon.Infrastructure.Providers.Reporting;
using RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var services = builder.Services;

services.AddControllers();
services.AddApiProblemDetails();
services.AddOpenApi("v1", options =>
    options.AddOperationTransformer<ApiKeySecurityOperationTransformer>());

// Kimlik doğrulama: varsayılan kapalı (default-deny). Anahtar yapılandırılmazsa uygulama açılmaz.
services.AddOptions<ApiKeyOptions>()
    .Bind(configuration.GetSection(ApiKeyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

services.AddAuthentication(ApiKeyDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyDefaults.Scheme, configureOptions: null);
services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Altyapı: konum önbelleği (MongoDB) + dış sağlayıcılar + sağlık kontrolleri.
services.AddMongoGeocodeCache(configuration);
services.AddGeoapifyProviders(configuration);
services.AddProviderHealthChecks();

// BR-4: geocoding'den önce kalıcı önbellek; isabet varsa dış sağlayıcı çağrılmaz.
// Uygulama servisleri (durumsuz).
services.AddTransient<VenueGeocodingService>();
services.AddTransient<HotelGeocodingService>();
services.AddTransient<DistanceRoutingService>();
services.AddTransient<RankingService>();
services.AddTransient<RecommendationDeliveryService>();
services.AddTransient<HotelRecommendationOrchestrator>();

// Belge üretimi (0006): rapor modeli + PDF renderer (QuestPDF) + şematik coğrafi görsel (SkiaSharp).
services.AddOptions<ReportingOptions>()
    .Bind(configuration.GetSection(ReportingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
services.AddQuestPdfReporting();
services.AddStaticMapWithFallback(configuration);
services.AddOpenAiCompatibleRecommendationExplanation(configuration);
services.AddTransient(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OpenAiCompatibleOptions>>().Value;
    return new RecommendationExplanationService(
        serviceProvider.GetRequiredService<IRecommendationExplanationProvider>(),
        new RecommendationExplanationOptions(options.Enabled));
});
services.AddTransient(serviceProvider => new RecommendationReportService(
    serviceProvider.GetRequiredService<IStaticMapProvider>(),
    serviceProvider.GetRequiredService<IRecommendationReportRenderer>(),
    serviceProvider.GetRequiredService<IWalkingRouteDistanceProvider>(),
    serviceProvider.GetRequiredService<RecommendationExplanationService>(),
    serviceProvider.GetRequiredService<IOptions<ReportingOptions>>().Value));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/{documentName}.json")
        .AllowAnonymous();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "RPAOtelRezervasyon API v1"));
}
else
{
    app.MapGet("/openapi/{**path}", () => Results.NotFound()).AllowAnonymous();
    app.MapGet("/swagger/{**path}", () => Results.NotFound()).AllowAnonymous();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "RPAOtelRezervasyon", status = "ok" }))
    .AllowAnonymous();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous();
app.MapHealthChecks("/health/providers", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains(ProviderHealthChecks.ProviderTag),
})
    .AllowAnonymous();
app.MapHealthChecks("/health/storage", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("storage"),
})
    .AllowAnonymous();
app.MapControllers();

app.Run();

/// <summary>Entegrasyon testlerinin (WebApplicationFactory) uygulamaya erişmesi için.</summary>
public partial class Program;
