using SentinelAI.Core.Enums;

namespace SentinelAI.Core.Entities;

/// <summary>
/// Represents a tenant (financial institution) in the system
/// </summary>
public class Tenant : AuditableEntity
{
    public required string Name { get; set; }
    public required string Code { get; set; }  // Unique tenant identifier
    public string? Description { get; set; }
    public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Starter;
    public bool IsActive { get; set; } = true;
    
    // API Access
    public string? ApiKeyHash { get; set; }  // Hashed API key
    public DateTime? ApiKeyExpiresAt { get; set; }
    
    // Configuration
    public string? WebhookUrl { get; set; }
    public string? WebhookSecret { get; set; }
    
    // Enabled modules (stored as JSON array of ModuleType)
    public List<ModuleType> EnabledModules { get; set; } = new();
    
    // Risk thresholds
    public decimal LowRiskThreshold { get; set; } = 0.3m;
    public decimal MediumRiskThreshold { get; set; } = 0.5m;
    public decimal HighRiskThreshold { get; set; } = 0.7m;
    public decimal CriticalRiskThreshold { get; set; } = 0.9m;
    
    // Rate limits
    public int RequestsPerMinute { get; set; } = 1000;
    public int RequestsPerDay { get; set; } = 100000;
    
    // Contact info (encrypted)
    public string? EncryptedContactEmail { get; set; }
    public string? EncryptedContactPhone { get; set; }
    
    // Navigation
    public virtual ICollection<TenantUser> Users { get; set; } = new List<TenantUser>();
    public virtual ICollection<FraudAlert> Alerts { get; set; } = new List<FraudAlert>();
}

/// <summary>
/// Represents a user belonging to a tenant
/// </summary>
public class TenantUser : AuditableEntity
{
    public Guid? TenantId { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string FullName { get; set; }
    public string? Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginAttempts { get; set; } = 0;

    // Lockout and refresh token support
    public DateTimeOffset? LockoutEnd { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }

    // Convenience properties for compatibility with FirstName/LastName usage
    public string FirstName
    {
        get
        {
            var parts = (FullName ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : string.Empty;
        }
        set
        {
            var last = LastName;
            FullName = string.IsNullOrWhiteSpace(last) ? value : $"{value} {last}";
        }
    }

    public string LastName
    {
        get
        {
            var parts = (FullName ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2 ? parts[1] : string.Empty;
        }
        set
        {
            var first = FirstName;
            FullName = string.IsNullOrWhiteSpace(first) ? value : $"{first} {value}";
        }
    }

    // Navigation
    public virtual Tenant? Tenant { get; set; }
}
