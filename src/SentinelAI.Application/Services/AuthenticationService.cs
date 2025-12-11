using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Entities;
using SentinelAI.Core.Exceptions;
using SentinelAI.Core.Interfaces;

namespace SentinelAI.Application.Services;

/// <summary>
/// Authentication service implementation
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<AuthenticationService> _logger;
    
    public AuthenticationService(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        IEncryptionService encryptionService,
        ILogger<AuthenticationService> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _encryptionService = encryptionService;
        _logger = logger;
    }
    
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Login attempt for user: {Email}", request.Email);
        
        // Find user by email
        var user = await _unitOfWork.TenantUsers.FirstOrDefaultAsync(
            u => u.Email.ToLower() == request.Email.ToLower(),
            cancellationToken);
        
        if (user == null)
        {
            _logger.LogWarning("Login failed: User not found for email {Email}", request.Email);
            throw new UnauthorizedException("Invalid email or password");
        }
        
        // Check if user is active
        if (!user.IsActive)
        {
            _logger.LogWarning("Login failed: User {UserId} is inactive", user.Id);
            throw new UnauthorizedException("Account is disabled. Please contact your administrator.");
        }
        
        // Check if account is locked
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
        {
            _logger.LogWarning("Login failed: User {UserId} is locked out until {LockoutEnd}", user.Id, user.LockoutEnd);
            throw new UnauthorizedException($"Account is locked. Please try again after {user.LockoutEnd:g}");
        }
        
        // Verify password
        if (!VerifyPassword(request.Password, user.PasswordHash))
        {
            // Increment failed login count
            user.FailedLoginAttempts++;
            
            // Lock account after 5 failed attempts
            if (user.FailedLoginAttempts >= 5)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(30);
                _logger.LogWarning("User {UserId} locked out due to too many failed login attempts", user.Id);
            }
            
            await _unitOfWork.TenantUsers.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            throw new UnauthorizedException("Invalid email or password");
        }
        
        // Verify tenant if provided
        Tenant? tenant = null;
        if (user.TenantId != null)
        {
            tenant = await _unitOfWork.Tenants.GetByIdAsync(user.TenantId.Value, cancellationToken);
            if (tenant == null || !tenant.IsActive)
            {
                throw new UnauthorizedException("Tenant is not active");
            }
        }
        
        // Reset failed login attempts
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;
        
        await _unitOfWork.TenantUsers.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user, tenant);
        var refreshToken = GenerateRefreshToken();
        
        // Store refresh token (hashed)
        user.RefreshToken = _encryptionService.Hash(refreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        
        await _unitOfWork.TenantUsers.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("User {UserId} logged in successfully", user.Id);
        
        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 3600, // 1 hour
            User = new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role ?? string.Empty,
                TenantId = user.TenantId,
                TenantName = tenant?.Name,
                Permissions = GetPermissionsForRole(user.Role ?? string.Empty)
            }
        };
    }
    
    public async Task<LoginResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var refreshTokenHash = _encryptionService.Hash(refreshToken);
        
        var user = await _unitOfWork.TenantUsers.FirstOrDefaultAsync(
            u => u.RefreshToken == refreshTokenHash && u.RefreshTokenExpiryTime > DateTime.UtcNow,
            cancellationToken);
        
        if (user == null)
        {
            throw new UnauthorizedException("Invalid or expired refresh token");
        }
        
        if (!user.IsActive)
        {
            throw new UnauthorizedException("Account is disabled");
        }
        
        Tenant? tenant = null;
        if (user.TenantId != null)
        {
            tenant = await _unitOfWork.Tenants.GetByIdAsync(user.TenantId.Value, cancellationToken);
        }
        
        // Generate new tokens
        var newAccessToken = _tokenService.GenerateAccessToken(user, tenant);
        var newRefreshToken = GenerateRefreshToken();
        
        // Update refresh token
        user.RefreshToken = _encryptionService.Hash(newRefreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        
        await _unitOfWork.TenantUsers.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return new LoginResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = 3600,
            User = new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role ?? string.Empty,
                TenantId = user.TenantId,
                TenantName = tenant?.Name,
                Permissions = GetPermissionsForRole(user.Role ?? string.Empty)
            }
        };
    }
    
    public async Task<ApiKeyValidationResponse> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        // Hash the API key for comparison
        var apiKeyHash = _encryptionService.Hash(apiKey);
        
        var tenant = await _unitOfWork.Tenants.FirstOrDefaultAsync(
            t => t.ApiKeyHash == apiKeyHash,
            cancellationToken);
        
        if (tenant == null)
        {
            return new ApiKeyValidationResponse { IsValid = false };
        }
        
        // Check if API key is expired
        if (tenant.ApiKeyExpiresAt.HasValue && tenant.ApiKeyExpiresAt < DateTime.UtcNow)
        {
            return new ApiKeyValidationResponse { IsValid = false };
        }
        
        if (!tenant.IsActive)
        {
            return new ApiKeyValidationResponse { IsValid = false };
        }
        
        return new ApiKeyValidationResponse
        {
            IsValid = true,
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            Permissions = GetTenantPermissions(tenant),
            ExpiresAt = tenant.ApiKeyExpiresAt
        };
    }
    
    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        // Check if email already exists
        var existingUser = await _unitOfWork.TenantUsers.FirstOrDefaultAsync(
            u => u.Email.ToLower() == request.Email.ToLower(),
            cancellationToken);
        
        if (existingUser != null)
        {
            throw new ConflictException($"User with email '{request.Email}' already exists");
        }
        
        // Verify tenant exists if provided
        Tenant? tenant = null;
        if (request.TenantId.HasValue)
        {
            tenant = await _unitOfWork.Tenants.GetByIdAsync(request.TenantId.Value, cancellationToken);
            if (tenant == null)
            {
                throw new EntityNotFoundException("Tenant", request.TenantId.Value);
            }
        }
        
        var user = new TenantUser
        {
            Email = request.Email.ToLower(),
            PasswordHash = HashPassword(request.Password),
            FullName = $"{request.FirstName} {request.LastName}",
            Role = request.Role,
            TenantId = request.TenantId,
            IsActive = true
        };
        
        await _unitOfWork.TenantUsers.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Created new user {UserId} with email {Email}", user.Id, user.Email);
        
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            TenantId = user.TenantId,
            TenantName = tenant?.Name,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        };
    }
    
    public async Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.TenantUsers.GetByIdAsync(userId, cancellationToken);
        if (user == null) return null;
        
        Tenant? tenant = null;
        if (user.TenantId != null)
        {
            tenant = await _unitOfWork.Tenants.GetByIdAsync(user.TenantId.Value, cancellationToken);
        }
        
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role ?? string.Empty,
            TenantId = user.TenantId,
            TenantName = tenant?.Name,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }
    
    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.TenantUsers.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new EntityNotFoundException("User", userId);
        }
        
        // Verify current password
        if (!VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedException("Current password is incorrect");
        }
        
        // Update password
        user.PasswordHash = HashPassword(request.NewPassword);
        user.RefreshToken = null; // Invalidate all sessions
        user.RefreshTokenExpiryTime = null;
        
        await _unitOfWork.TenantUsers.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Password changed for user {UserId}", userId);
        
        return true;
    }
    
    public async Task RevokeAllTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.TenantUsers.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new EntityNotFoundException("User", userId);
        }
        
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        
        await _unitOfWork.TenantUsers.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("All tokens revoked for user {UserId}", userId);
    }
    
    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var refreshTokenHash = _encryptionService.Hash(refreshToken);
        
        var user = await _unitOfWork.TenantUsers.FirstOrDefaultAsync(
            u => u.RefreshToken == refreshTokenHash,
            cancellationToken);
        
        if (user != null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            
            await _unitOfWork.TenantUsers.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("User {UserId} logged out", user.Id);
        }
    }
    
    private static string HashPassword(string password)
    {
        // Use BCrypt-style hashing with PBKDF2
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            100000,
            HashAlgorithmName.SHA256,
            32);
        
        // Combine salt and hash
        byte[] combined = new byte[salt.Length + hash.Length];
        Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
        Buffer.BlockCopy(hash, 0, combined, salt.Length, hash.Length);
        
        return Convert.ToBase64String(combined);
    }
    
    private static bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            byte[] combined = Convert.FromBase64String(storedHash);
            
            // Extract salt (first 16 bytes)
            byte[] salt = new byte[16];
            Buffer.BlockCopy(combined, 0, salt, 0, 16);
            
            // Extract stored hash (remaining bytes)
            byte[] storedHashBytes = new byte[combined.Length - 16];
            Buffer.BlockCopy(combined, 16, storedHashBytes, 0, storedHashBytes.Length);
            
            // Compute hash with same salt
            byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                100000,
                HashAlgorithmName.SHA256,
                32);
            
            // Use constant-time comparison
            return CryptographicOperations.FixedTimeEquals(storedHashBytes, computedHash);
        }
        catch
        {
            return false;
        }
    }
    
    private static string GenerateRefreshToken()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
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
    
    private static List<string> GetTenantPermissions(Tenant tenant)
    {
        return new List<string>
        {
            Core.Constants.Permissions.AnalyzeTransactions,
            Core.Constants.Permissions.ViewAlerts,
            Core.Constants.Permissions.ManageAlerts
        };
    }
}
