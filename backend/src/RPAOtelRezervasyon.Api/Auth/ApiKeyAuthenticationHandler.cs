using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace RPAOtelRezervasyon.Api.Auth;

/// <summary>API anahtarı ayarları (bölüm: <c>Api</c>). Anahtar sırdır; `appsettings` yerine ortam değişkeni/user-secrets ile verilir.</summary>
public sealed class ApiKeyOptions
{
    public const string SectionName = "Api";

    [Required(AllowEmptyStrings = false, ErrorMessage = "Api:ApiKey zorunludur.")]
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>Kimlik doğrulama şeması ve başlık sabitleri.</summary>
public static class ApiKeyDefaults
{
    public const string Scheme = "ApiKey";

    public const string HeaderName = "X-Api-Key";
}

/// <summary>
/// Basit API anahtarı kimlik doğrulaması (spec G-5: V1'de API anahtarı, JWT ertelendi). Anahtar
/// karşılaştırması zamanlama saldırısına kapalı olacak şekilde sabit zamanlıdır; başarısızlıkta
/// ayrıntı sızdırılmaz (docs/security.md).
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<ApiKeyOptions> apiKeyOptions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private readonly ApiKeyOptions _apiKeyOptions = apiKeyOptions.Value;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyDefaults.HeaderName, out var provided) || provided.Count != 1)
        {
            return Task.FromResult(AuthenticateResult.Fail("API anahtarı eksik."));
        }

        var candidate = provided[0] ?? string.Empty;
        if (string.IsNullOrEmpty(candidate) || !FixedTimeEquals(candidate, _apiKeyOptions.ApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("API anahtarı geçersiz."));
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "api-client")], Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// 401 yanıtı gövdesiz bırakılmaz: yayınlanan OpenAPI belgesi ve docs/conventions.md tek hata
    /// deseni olarak ProblemDetails sözü verir; challenge da aynı biçimi döner (0002 AC-4).
    /// </summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;

        return Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Kimlik doğrulaması gerekli",
                Detail = $"Geçerli bir {ApiKeyDefaults.HeaderName} başlığı sağlayın.",
            },
            options: null,
            contentType: "application/problem+json");
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
