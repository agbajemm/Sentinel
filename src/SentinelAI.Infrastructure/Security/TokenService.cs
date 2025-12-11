using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SentinelAI.Core.Interfaces;

namespace SentinelAI.Infrastructure.Security;

/// <summary>
/// JWT configuration settings
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";
    
    public required string Secret { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public int ExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}

/// <summary>
/// JWT token service implementation
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtSettings _settings;
    private readonly byte[] _key;
    
    public TokenService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;
        _key = Encoding.UTF8.GetBytes(_settings.Secret);
    }
    
    /// <inheritdoc/>
    public string GenerateToken(Guid userId, Guid tenantId, string email, string role, IEnumerable<string>? permissions = null)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(Core.Constants.ClaimTypes.UserId, userId.ToString()),
            new(Core.Constants.ClaimTypes.TenantId, tenantId.ToString()),
            new(Core.Constants.ClaimTypes.Role, role)
        };
        
        if (permissions != null)
        {
            claims.AddRange(permissions.Select(p => new Claim(Core.Constants.ClaimTypes.Permissions, p)));
        }
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_settings.ExpirationMinutes),
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_key),
                SecurityAlgorithms.HmacSha256Signature)
        };
        
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
    
    /// <inheritdoc/>
    public string GenerateAccessToken(Core.Entities.TenantUser user, Core.Entities.Tenant? tenant)
    {
        var permissions = GetPermissionsForRole(user.Role ?? string.Empty);
        
        return GenerateToken(
            user.Id,
            user.TenantId ?? Guid.Empty,
            user.Email,
            user.Role ?? string.Empty,
            permissions);
    }
    
    private static List<string> GetPermissionsForRole(string role)
    {
        return role switch
        {
            Core.Constants.Roles.SuperAdmin => new List<string>
            {
                Core.Constants.Permissions.AnalyzeTransactions,
                Core.Constants.Permissions.ViewAlerts,
                Core.Constants.Permissions.ManageAlerts,
                Core.Constants.Permissions.ManageTenants,
                Core.Constants.Permissions.ManageUsers,
                Core.Constants.Permissions.ViewReports,
                Core.Constants.Permissions.ManageSettings,
                Core.Constants.Permissions.ViewAuditLogs
            },
            Core.Constants.Roles.TenantAdmin => new List<string>
            {
                Core.Constants.Permissions.AnalyzeTransactions,
                Core.Constants.Permissions.ViewAlerts,
                Core.Constants.Permissions.ManageAlerts,
                Core.Constants.Permissions.ManageUsers,
                Core.Constants.Permissions.ViewReports,
                Core.Constants.Permissions.ManageSettings
            },
            Core.Constants.Roles.FraudAnalyst => new List<string>
            {
                Core.Constants.Permissions.AnalyzeTransactions,
                Core.Constants.Permissions.ViewAlerts,
                Core.Constants.Permissions.ManageAlerts,
                Core.Constants.Permissions.ViewReports
            },
            Core.Constants.Roles.Investigator => new List<string>
            {
                Core.Constants.Permissions.ViewAlerts,
                Core.Constants.Permissions.ManageAlerts,
                Core.Constants.Permissions.ViewReports
            },
            Core.Constants.Roles.Viewer => new List<string>
            {
                Core.Constants.Permissions.ViewAlerts,
                Core.Constants.Permissions.ViewReports
            },
            _ => new List<string>()
        };
    }
    
    /// <inheritdoc/>
    public Core.Interfaces.TokenValidationResult ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_key),
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
            
            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            
            var userIdClaim = principal.FindFirst(Core.Constants.ClaimTypes.UserId)?.Value;
            var tenantIdClaim = principal.FindFirst(Core.Constants.ClaimTypes.TenantId)?.Value;
            var emailClaim = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
            var roleClaim = principal.FindFirst(Core.Constants.ClaimTypes.Role)?.Value;
            var permissionClaims = principal.FindAll(Core.Constants.ClaimTypes.Permissions)
                .Select(c => c.Value).ToList();
            
            return new Core.Interfaces.TokenValidationResult
            {
                IsValid = true,
                UserId = Guid.TryParse(userIdClaim, out var userId) ? userId : null,
                TenantId = Guid.TryParse(tenantIdClaim, out var tenantId) ? tenantId : null,
                Email = emailClaim,
                Role = roleClaim,
                Permissions = permissionClaims
            };
        }
        catch (SecurityTokenExpiredException)
        {
            return new Core.Interfaces.TokenValidationResult { IsValid = false, ErrorMessage = "Token has expired." };
        }
        catch (Exception ex)
        {
            return new Core.Interfaces.TokenValidationResult { IsValid = false, ErrorMessage = ex.Message };
        }
    }
    
    /// <inheritdoc/>
    public string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }
}

/// <summary>
/// API key service implementation
/// </summary>
public class ApiKeyService : IApiKeyService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEncryptionService _encryptionService;
    
    public ApiKeyService(IUnitOfWork unitOfWork, IEncryptionService encryptionService)
    {
        _unitOfWork = unitOfWork;
        _encryptionService = encryptionService;
    }
    
    /// <inheritdoc/>
    public (string apiKey, string apiKeyHash) GenerateApiKey()
    {
        // Generate a secure random API key
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        var apiKey = $"sk_live_{Convert.ToBase64String(keyBytes).Replace("+", "").Replace("/", "").Replace("=", "")}";
        var apiKeyHash = _encryptionService.Hash(apiKey);
        
        return (apiKey, apiKeyHash);
    }
    
    /// <inheritdoc/>
    public async Task<Guid?> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(apiKey))
            return null;
        
        var apiKeyHash = _encryptionService.Hash(apiKey);
        
        var tenant = await _unitOfWork.Tenants.FirstOrDefaultAsync(
            t => t.ApiKeyHash == apiKeyHash && 
                 t.IsActive && 
                 !t.IsDeleted &&
                 (t.ApiKeyExpiresAt == null || t.ApiKeyExpiresAt > DateTime.UtcNow),
            cancellationToken);
        
        return tenant?.Id;
    }
    
    /// <inheritdoc/>
    public async Task<string> RotateApiKeyAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new Core.Exceptions.EntityNotFoundException("Tenant", tenantId);
        
        var (newApiKey, newApiKeyHash) = GenerateApiKey();
        
        tenant.ApiKeyHash = newApiKeyHash;
        tenant.ApiKeyExpiresAt = DateTime.UtcNow.AddYears(1);
        tenant.UpdatedAt = DateTime.UtcNow;
        
        await _unitOfWork.Tenants.UpdateAsync(tenant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return newApiKey;
    }
    
    /// <inheritdoc/>
    public async Task RevokeApiKeyAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new Core.Exceptions.EntityNotFoundException("Tenant", tenantId);
        
        tenant.ApiKeyHash = null;
        tenant.ApiKeyExpiresAt = null;
        tenant.UpdatedAt = DateTime.UtcNow;
        
        await _unitOfWork.Tenants.UpdateAsync(tenant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Data masking service implementation
/// </summary>
public class DataMaskingService : IDataMaskingService
{
    /// <inheritdoc/>
    public string MaskAccountNumber(string accountNumber)
    {
        if (string.IsNullOrEmpty(accountNumber) || accountNumber.Length < 4)
            return "****";
        
        return $"****{accountNumber[^4..]}";
    }
    
    /// <inheritdoc/>
    public string MaskPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 4)
            return "****";
        
        var digitsOnly = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length < 4)
            return "****";
        
        return $"***{digitsOnly[^4..]}";
    }
    
    /// <inheritdoc/>
    public string MaskEmail(string email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            return "***@***.***";
        
        var parts = email.Split('@');
        var localPart = parts[0];
        var domainPart = parts[1];
        
        var maskedLocal = localPart.Length > 2 
            ? $"{localPart[0]}***{localPart[^1..]}" 
            : "***";
        
        var domainParts = domainPart.Split('.');
        var maskedDomain = domainParts.Length > 1
            ? $"***.{domainParts[^1]}"
            : "***";
        
        return $"{maskedLocal}@{maskedDomain}";
    }
    
    /// <inheritdoc/>
    public string MaskBvn(string bvn)
    {
        if (string.IsNullOrEmpty(bvn) || bvn.Length < 4)
            return "***********";
        
        return $"*******{bvn[^4..]}";
    }
    
    /// <inheritdoc/>
    public string MaskSensitiveJson(string json, IEnumerable<string> sensitiveFields)
    {
        // Simple implementation - for production, use a proper JSON library
        var result = json;
        foreach (var field in sensitiveFields)
        {
            // This is a simplified approach - in production, parse and modify the JSON properly
            var pattern = $"\"{field}\":\\s*\"[^\"]*\"";
            result = System.Text.RegularExpressions.Regex.Replace(
                result, 
                pattern, 
                $"\"{field}\": \"***MASKED***\"",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return result;
    }
}
