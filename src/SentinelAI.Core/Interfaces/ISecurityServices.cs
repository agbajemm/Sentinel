namespace SentinelAI.Core.Interfaces;

/// <summary>
/// Encryption service for sensitive data protection
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts plaintext using AES-256
    /// </summary>
    string Encrypt(string plainText);
    
    /// <summary>
    /// Decrypts ciphertext
    /// </summary>
    string Decrypt(string cipherText);
    
    /// <summary>
    /// Creates a one-way hash for lookup purposes
    /// </summary>
    string Hash(string input);
    
    /// <summary>
    /// Verifies a value against its hash
    /// </summary>
    bool VerifyHash(string input, string hash);
    
    /// <summary>
    /// Generates a secure random key
    /// </summary>
    string GenerateSecureKey(int length = 32);
    
    /// <summary>
    /// Encrypts sensitive data with a tenant-specific key
    /// </summary>
    string EncryptForTenant(string plainText, Guid tenantId);
    
    /// <summary>
    /// Decrypts tenant-specific encrypted data
    /// </summary>
    string DecryptForTenant(string cipherText, Guid tenantId);
}

/// <summary>
/// API key management service
/// </summary>
public interface IApiKeyService
{
    /// <summary>
    /// Generates a new API key
    /// </summary>
    (string apiKey, string apiKeyHash) GenerateApiKey();
    
    /// <summary>
    /// Validates an API key and returns the tenant ID if valid
    /// </summary>
    Task<Guid?> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Rotates an API key for a tenant
    /// </summary>
    Task<string> RotateApiKeyAsync(Guid tenantId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Revokes an API key
    /// </summary>
    Task RevokeApiKeyAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// JWT token service for authentication
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a JWT token for a user
    /// </summary>
    string GenerateToken(Guid userId, Guid tenantId, string email, string role, IEnumerable<string>? permissions = null);
    
    /// <summary>
    /// Generates a JWT access token for a user entity
    /// </summary>
    string GenerateAccessToken(Entities.TenantUser user, Entities.Tenant? tenant);
    
    /// <summary>
    /// Validates a token and extracts claims
    /// </summary>
    TokenValidationResult ValidateToken(string token);
    
    /// <summary>
    /// Generates a refresh token
    /// </summary>
    string GenerateRefreshToken();
}

/// <summary>
/// Result of token validation
/// </summary>
public class TokenValidationResult
{
    public bool IsValid { get; set; }
    public Guid? UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public IEnumerable<string>? Permissions { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Data masking service for logs and exports
/// </summary>
public interface IDataMaskingService
{
    /// <summary>
    /// Masks an account number (shows only last 4 digits)
    /// </summary>
    string MaskAccountNumber(string accountNumber);
    
    /// <summary>
    /// Masks a phone number
    /// </summary>
    string MaskPhoneNumber(string phoneNumber);
    
    /// <summary>
    /// Masks an email address
    /// </summary>
    string MaskEmail(string email);
    
    /// <summary>
    /// Masks a BVN
    /// </summary>
    string MaskBvn(string bvn);
    
    /// <summary>
    /// Masks sensitive fields in a JSON object
    /// </summary>
    string MaskSensitiveJson(string json, IEnumerable<string> sensitiveFields);
}
