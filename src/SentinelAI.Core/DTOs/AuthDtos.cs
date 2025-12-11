using System.ComponentModel.DataAnnotations;

namespace SentinelAI.Core.DTOs;

/// <summary>
/// Login request DTO
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// User email address
    /// </summary>
    [Required]
    [EmailAddress]
    public required string Email { get; set; }
    
    /// <summary>
    /// User password
    /// </summary>
    [Required]
    [MinLength(8)]
    public required string Password { get; set; }
    
    /// <summary>
    /// Tenant code (optional for super admin)
    /// </summary>
    public string? TenantCode { get; set; }
}

/// <summary>
/// Login response DTO
/// </summary>
public class LoginResponse
{
    /// <summary>
    /// JWT access token
    /// </summary>
    public required string AccessToken { get; set; }
    
    /// <summary>
    /// Refresh token for obtaining new access tokens
    /// </summary>
    public required string RefreshToken { get; set; }
    
    /// <summary>
    /// Token type (always "Bearer")
    /// </summary>
    public string TokenType { get; set; } = "Bearer";
    
    /// <summary>
    /// Access token expiration time in seconds
    /// </summary>
    public int ExpiresIn { get; set; }
    
    /// <summary>
    /// User information
    /// </summary>
    public UserInfo? User { get; set; }
}

/// <summary>
/// User information included in login response
/// </summary>
public class UserInfo
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Role { get; set; }
    public Guid? TenantId { get; set; }
    public string? TenantName { get; set; }
    public List<string> Permissions { get; set; } = new();
}

/// <summary>
/// Refresh token request DTO
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// The refresh token
    /// </summary>
    [Required]
    public required string RefreshToken { get; set; }
}

/// <summary>
/// API key validation request
/// </summary>
public class ValidateApiKeyRequest
{
    /// <summary>
    /// The API key to validate
    /// </summary>
    [Required]
    public required string ApiKey { get; set; }
}

/// <summary>
/// API key validation response
/// </summary>
public class ApiKeyValidationResponse
{
    public bool IsValid { get; set; }
    public Guid? TenantId { get; set; }
    public string? TenantName { get; set; }
    public List<string> Permissions { get; set; } = new();
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Change password request
/// </summary>
public class ChangePasswordRequest
{
    [Required]
    public required string CurrentPassword { get; set; }
    
    [Required]
    [MinLength(8)]
    public required string NewPassword { get; set; }
    
    [Required]
    [Compare(nameof(NewPassword))]
    public required string ConfirmPassword { get; set; }
}

/// <summary>
/// Create user request
/// </summary>
public class CreateUserRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }
    
    [Required]
    [MinLength(8)]
    public required string Password { get; set; }
    
    [Required]
    public required string FirstName { get; set; }
    
    [Required]
    public required string LastName { get; set; }
    
    [Required]
    public required string Role { get; set; }
    
    public Guid? TenantId { get; set; }
}

/// <summary>
/// User DTO for responses
/// </summary>
public class UserDto
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Role { get; set; }
    public Guid? TenantId { get; set; }
    public string? TenantName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
