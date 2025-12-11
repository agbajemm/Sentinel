using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SentinelAI.Core.Interfaces;

namespace SentinelAI.Api.Middleware;

/// <summary>
/// API Key authentication handler for service-to-service calls
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private const string ApiKeyHeaderName = "X-API-Key";
    private readonly IApiKeyService _apiKeyService;
    
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService apiKeyService)
        : base(options, logger, encoder)
    {
        _apiKeyService = apiKeyService;
    }
    
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for API key in header
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyValue))
        {
            return AuthenticateResult.NoResult();
        }
        
        var apiKey = apiKeyValue.ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.Fail("Invalid API key");
        }
        
        // Validate API key
        var tenantId = await _apiKeyService.ValidateApiKeyAsync(apiKey);
        
        if (!tenantId.HasValue)
        {
            Logger.LogWarning("Invalid API key attempted: {ApiKeyPrefix}...", 
                apiKey.Length > 10 ? apiKey[..10] : apiKey);
            return AuthenticateResult.Fail("Invalid API key");
        }
        
        // Create claims
        var claims = new[]
        {
            new Claim(Core.Constants.ClaimTypes.TenantId, tenantId.Value.ToString()),
            new Claim(ClaimTypes.AuthenticationMethod, "ApiKey"),
            new Claim(ClaimTypes.Role, Core.Constants.Roles.TenantAdmin)
        };
        
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        
        Logger.LogDebug("API key authenticated for tenant {TenantId}", tenantId.Value);
        
        return AuthenticateResult.Success(ticket);
    }
}

/// <summary>
/// Options for API key authentication
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

/// <summary>
/// Extension methods for API key authentication
/// </summary>
public static class ApiKeyAuthenticationExtensions
{
    public const string SchemeName = "ApiKey";
    
    public static AuthenticationBuilder AddApiKeyAuthentication(
        this AuthenticationBuilder builder,
        Action<ApiKeyAuthenticationOptions>? configureOptions = null)
    {
        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            SchemeName,
            configureOptions ?? (_ => { }));
    }
}
