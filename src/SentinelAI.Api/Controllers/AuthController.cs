using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Interfaces;

namespace SentinelAI.Api.Controllers;

/// <summary>
/// Authentication controller for login, token refresh, and user management
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<AuthController> _logger;
    
    public AuthController(
        IAuthenticationService authService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }
    
    /// <summary>
    /// Authenticates a user and returns JWT tokens
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JWT access token and refresh token</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Login attempt for {Email}", request.Email);
        
        var result = await _authService.LoginAsync(request, cancellationToken);
        
        return Ok(ApiResponse<LoginResponse>.Ok(result, "Login successful"));
    }
    
    /// <summary>
    /// Refreshes an access token using a refresh token
    /// </summary>
    /// <param name="request">Refresh token request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New JWT access token and refresh token</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken, cancellationToken);
        
        return Ok(ApiResponse<LoginResponse>.Ok(result, "Token refreshed successfully"));
    }
    
    /// <summary>
    /// Logs out the current user by revoking the refresh token
    /// </summary>
    /// <param name="request">Refresh token to revoke</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success message</returns>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object?>>> Logout(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Logged out successfully"));
    }
    
    /// <summary>
    /// Gets the current user's profile
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current user information</returns>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromClaims();
        if (userId == Guid.Empty)
        {
            return Unauthorized(ApiResponse<object>.Fail("Invalid token"));
        }
        
        var user = await _authService.GetUserAsync(userId, cancellationToken);
        if (user == null)
        {
            return NotFound(ApiResponse<object>.Fail("User not found"));
        }
        
        return Ok(ApiResponse<UserDto>.Ok(user));
    }
    
    /// <summary>
    /// Changes the current user's password
    /// </summary>
    /// <param name="request">Password change request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success message</returns>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object?>>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromClaims();
        if (userId == Guid.Empty)
        {
            return Unauthorized(ApiResponse<object?>.Fail("Invalid token"));
        }

        await _authService.ChangePasswordAsync(userId, request, cancellationToken);

        return Ok(ApiResponse<object?>.Ok(null, "Password changed successfully"));
    }
    
    /// <summary>
    /// Creates a new user (Admin only)
    /// </summary>
    /// <param name="request">User creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created user information</returns>
    [HttpPost("users")]
    [Authorize(Policy = "TenantAdmin")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<UserDto>>> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _authService.CreateUserAsync(request, cancellationToken);
        
        return CreatedAtAction(
            nameof(GetUser),
            new { userId = user.Id },
            ApiResponse<UserDto>.Ok(user, "User created successfully"));
    }
    
    /// <summary>
    /// Gets a user by ID (Admin only)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User information</returns>
    [HttpGet("users/{userId:guid}")]
    [Authorize(Policy = "TenantAdmin")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUser(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _authService.GetUserAsync(userId, cancellationToken);
        if (user == null)
        {
            return NotFound(ApiResponse<object>.Fail("User not found"));
        }
        
        return Ok(ApiResponse<UserDto>.Ok(user));
    }
    
    /// <summary>
    /// Revokes all refresh tokens for a user (Admin only)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success message</returns>
    [HttpPost("users/{userId:guid}/revoke-tokens")]
    [Authorize(Policy = "TenantAdmin")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object?>>> RevokeUserTokens(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _authService.RevokeAllTokensAsync(userId, cancellationToken);

        return Ok(ApiResponse<object?>.Ok(null, "All tokens revoked successfully"));
    }
    
    /// <summary>
    /// Validates an API key
    /// </summary>
    /// <param name="request">API key to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>API key validation result</returns>
    [HttpPost("validate-api-key")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ApiKeyValidationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ApiKeyValidationResponse>>> ValidateApiKey(
        [FromBody] ValidateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.ValidateApiKeyAsync(request.ApiKey, cancellationToken);
        
        return Ok(ApiResponse<ApiKeyValidationResponse>.Ok(result));
    }
    
    private Guid GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(Core.Constants.ClaimTypes.UserId)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
