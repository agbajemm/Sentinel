using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SentinelAI.Core.Entities;
using SentinelAI.Core.Enums;
using SentinelAI.Core.Interfaces;
using SentinelAI.Infrastructure.Data;

namespace SentinelAI.Infrastructure.Data;

/// <summary>
/// Database seeder for initial data
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>
    /// Seeds the database with initial data
    /// </summary>
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SentinelDbContext>>();
        
        try
        {
            // Apply migrations
            await context.Database.MigrateAsync();
            
            // Seed data
            await SeedTenantsAsync(context, encryptionService, logger);
            await SeedUsersAsync(context, logger);
            await SeedAgentConfigurationsAsync(context, logger);
            
            logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding database");
            throw;
        }
    }
    
    private static async Task SeedTenantsAsync(
        SentinelDbContext context,
        IEncryptionService encryptionService,
        ILogger logger)
    {
        if (await context.Tenants.AnyAsync())
        {
            logger.LogInformation("Tenants already seeded, skipping...");
            return;
        }
        
        logger.LogInformation("Seeding tenants...");
        
        // Generate API key for demo tenant
        var apiKey = GenerateApiKey();
        var apiKeyHash = encryptionService.Hash(apiKey);
        
        var demoTenant = new Tenant
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Demo Bank",
            Code = "DEMOBANK",
            Description = "Demo tenant for testing and development",
            SubscriptionTier = SubscriptionTier.Enterprise,
            IsActive = true,
            EnabledModules = new List<ModuleType>
            {
                ModuleType.TransactionSentinel,
                ModuleType.POSAgentShield,
                ModuleType.IdentityFortress,
                ModuleType.InsiderThreat,
                ModuleType.NetworkIntelligence,
                ModuleType.InvestigationAssistant
            },
            ApiKeyHash = apiKeyHash,
            ApiKeyExpiresAt = DateTime.UtcNow.AddYears(1),
            WebhookUrl = "https://webhook.demo-bank.local/sentinel",
            EncryptedContactEmail = encryptionService.Encrypt("admin@demo-bank.local"),
            EncryptedContactPhone = encryptionService.Encrypt("+2348012345678"),
            LowRiskThreshold = 0.3m,
            MediumRiskThreshold = 0.5m,
            HighRiskThreshold = 0.7m,
            CriticalRiskThreshold = 0.9m,
            RequestsPerMinute = 1000,
            RequestsPerDay = 100000
        };
        
        await context.Tenants.AddAsync(demoTenant);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Demo tenant created. API Key (save this, shown only once): {ApiKey}", apiKey);
    }
    
    private static async Task SeedUsersAsync(SentinelDbContext context, ILogger logger)
    {
        if (await context.TenantUsers.AnyAsync())
        {
            logger.LogInformation("Users already seeded, skipping...");
            return;
        }
        
        logger.LogInformation("Seeding users...");
        
        var demoTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        
        // Create super admin (no tenant)
        var superAdmin = new TenantUser
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Email = "superadmin@sentinel-ai.local",
            PasswordHash = HashPassword("SuperAdmin123!"),
            FullName = "Super Admin",
            Role = Core.Constants.Roles.SuperAdmin,
            TenantId = null,
            IsActive = true
        };
        
        // Create tenant admin
        var tenantAdmin = new TenantUser
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Email = "admin@demo-bank.local",
            PasswordHash = HashPassword("TenantAdmin123!"),
            FullName = "Tenant Admin",
            Role = Core.Constants.Roles.TenantAdmin,
            TenantId = demoTenantId,
            IsActive = true
        };
        
        // Create fraud analyst
        var fraudAnalyst = new TenantUser
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
            Email = "analyst@demo-bank.local",
            PasswordHash = HashPassword("Analyst123!"),
            FullName = "Fraud Analyst",
            Role = Core.Constants.Roles.FraudAnalyst,
            TenantId = demoTenantId,
            IsActive = true
        };
        
        // Create investigator
        var investigator = new TenantUser
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000004"),
            Email = "investigator@demo-bank.local",
            PasswordHash = HashPassword("Investigator123!"),
            FullName = "Fraud Investigator",
            Role = Core.Constants.Roles.Investigator,
            TenantId = demoTenantId,
            IsActive = true
        };
        
        await context.TenantUsers.AddRangeAsync(superAdmin, tenantAdmin, fraudAnalyst, investigator);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Users created successfully");
        logger.LogInformation("Test credentials:");
        logger.LogInformation("  Super Admin: superadmin@sentinel-ai.local / SuperAdmin123!");
        logger.LogInformation("  Tenant Admin: admin@demo-bank.local / TenantAdmin123!");
        logger.LogInformation("  Analyst: analyst@demo-bank.local / Analyst123!");
        logger.LogInformation("  Investigator: investigator@demo-bank.local / Investigator123!");
    }
    
    private static async Task SeedAgentConfigurationsAsync(SentinelDbContext context, ILogger logger)
    {
        if (await context.AgentConfigurations.AnyAsync())
        {
            logger.LogInformation("Agent configurations already seeded, skipping...");
            return;
        }
        
        logger.LogInformation("Seeding agent configurations...");
        
        var agents = new List<AgentConfiguration>
        {
            new()
            {
                AgentId = Core.Constants.AgentIds.MasterOrchestrator,
                Name = "Master Orchestrator",
                Level = AgentLevel.Orchestration,
                Module = ModuleType.TransactionSentinel,
                IsActive = true,
                Version = 1
            },
            new()
            {
                AgentId = Core.Constants.AgentIds.TransactionAnalyzer,
                Name = "Transaction Analyzer",
                Level = AgentLevel.Specialist,
                Module = ModuleType.TransactionSentinel,
                IsActive = true,
                Version = 1
            },
            new()
            {
                AgentId = Core.Constants.AgentIds.BehaviorProfiler,
                Name = "Behavior Profiler",
                Level = AgentLevel.Specialist,
                Module = ModuleType.TransactionSentinel,
                IsActive = true,
                Version = 1
            },
            new()
            {
                AgentId = Core.Constants.AgentIds.RiskScorer,
                Name = "Risk Scorer",
                Level = AgentLevel.Specialist,
                Module = ModuleType.TransactionSentinel,
                IsActive = true,
                Version = 1
            },
            new()
            {
                AgentId = Core.Constants.AgentIds.TerminalMonitor,
                Name = "Terminal Monitor",
                Level = AgentLevel.Specialist,
                Module = ModuleType.POSAgentShield,
                IsActive = true,
                Version = 1
            },
            new()
            {
                AgentId = Core.Constants.AgentIds.MuleHunter,
                Name = "Mule Hunter",
                Level = AgentLevel.Specialist,
                Module = ModuleType.POSAgentShield,
                IsActive = true,
                Version = 1
            },
            new()
            {
                AgentId = Core.Constants.AgentIds.IdentityVerifier,
                Name = "Identity Verifier",
                Level = AgentLevel.Specialist,
                Module = ModuleType.IdentityFortress,
                IsActive = true,
                Version = 1
            },
            new()
            {
                AgentId = Core.Constants.AgentIds.SyntheticDetector,
                Name = "Synthetic Identity Detector",
                Level = AgentLevel.Specialist,
                Module = ModuleType.IdentityFortress,
                IsActive = true,
                Version = 1
            }
        };
        
        await context.AgentConfigurations.AddRangeAsync(agents);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Agent configurations seeded successfully");
    }
    
    private static string GenerateApiKey()
    {
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        return $"sk_live_{Convert.ToBase64String(keyBytes).Replace("+", "").Replace("/", "").Replace("=", "")}";
    }
    
    private static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            100000,
            HashAlgorithmName.SHA256,
            32);
        
        byte[] combined = new byte[salt.Length + hash.Length];
        Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
        Buffer.BlockCopy(hash, 0, combined, salt.Length, hash.Length);
        
        return Convert.ToBase64String(combined);
    }
}
