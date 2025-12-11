using SentinelAI.Core.DTOs;
using SentinelAI.Core.Entities;

namespace SentinelAI.Core.Interfaces;

/// <summary>
/// Authentication service interface
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticates a user with email and password
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Refreshes an access token using a refresh token
    /// </summary>
    Task<LoginResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates an API key and returns tenant information
    /// </summary>
    Task<ApiKeyValidationResponse> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new user
    /// </summary>
    Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets user by ID
    /// </summary>
    Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Changes user password
    /// </summary>
    Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Revokes all refresh tokens for a user
    /// </summary>
    Task RevokeAllTokensAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Logs out the current user (revokes current refresh token)
    /// </summary>
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
}
