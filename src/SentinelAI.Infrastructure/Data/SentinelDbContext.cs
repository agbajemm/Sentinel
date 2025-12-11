using Microsoft.EntityFrameworkCore;
using SentinelAI.Core.Entities;
using SentinelAI.Core.Enums;

namespace SentinelAI.Infrastructure.Data;

/// <summary>
/// Database context for Sentinel AI
/// </summary>
public class SentinelDbContext : DbContext
{
    public SentinelDbContext(DbContextOptions<SentinelDbContext> options) : base(options)
    {
    }
    
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<TransactionAnalysis> TransactionAnalyses => Set<TransactionAnalysis>();
    public DbSet<FraudAlert> FraudAlerts => Set<FraudAlert>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Core.Entities.AgentConfiguration> AgentConfigurations => Set<Core.Entities.AgentConfiguration>();
    public DbSet<AgentInvocationLog> AgentInvocationLogs => Set<AgentInvocationLog>();
    public DbSet<AgentMessage> AgentMessages => Set<AgentMessage>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Tenant configuration
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.ApiKeyHash);
            
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.WebhookUrl).HasMaxLength(500);
            entity.Property(e => e.WebhookSecret).HasMaxLength(256);
            entity.Property(e => e.ApiKeyHash).HasMaxLength(256);
            entity.Property(e => e.EncryptedContactEmail).HasMaxLength(500);
            entity.Property(e => e.EncryptedContactPhone).HasMaxLength(500);
            
            // Store enabled modules as JSON
            entity.Property(e => e.EnabledModules)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<ModuleType>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<ModuleType>())
                .HasColumnType("jsonb");
            
            entity.HasQueryFilter(e => !e.IsDeleted);
        });
        
        // TenantUser configuration
        modelBuilder.Entity<TenantUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.Email }).IsUnique();
            
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(50);
            
            entity.HasOne(e => e.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasQueryFilter(e => !e.IsDeleted);
        });
        
        // TransactionAnalysis configuration
        modelBuilder.Entity<TransactionAnalysis>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.TransactionReference);
            entity.HasIndex(e => e.ExternalTransactionId);
            entity.HasIndex(e => e.SourceAccountHash);
            entity.HasIndex(e => e.DestinationAccountHash);
            entity.HasIndex(e => e.CreatedAt);
            
            entity.Property(e => e.TransactionReference).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ExternalTransactionId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 4);
            entity.Property(e => e.RiskScore).HasPrecision(5, 4);
            entity.Property(e => e.EncryptedSourceAccount).HasMaxLength(500);
            entity.Property(e => e.EncryptedDestinationAccount).HasMaxLength(500);
            entity.Property(e => e.EncryptedCustomerId).HasMaxLength(500);
            entity.Property(e => e.SourceAccountHash).HasMaxLength(256);
            entity.Property(e => e.DestinationAccountHash).HasMaxLength(256);
            entity.Property(e => e.DeviceFingerprint).HasMaxLength(256);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.SessionId).HasMaxLength(256);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.Country).HasMaxLength(100);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.RiskFactors).HasColumnType("jsonb");
            entity.Property(e => e.Explanation).HasMaxLength(2000);
            entity.Property(e => e.ModulesInvoked).HasColumnType("jsonb");
            entity.Property(e => e.AgentsInvoked).HasColumnType("jsonb");
            
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasQueryFilter(e => !e.IsDeleted);
        });
        
        // FraudAlert configuration
        modelBuilder.Entity<FraudAlert>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.AlertReference).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            
            entity.Property(e => e.AlertReference).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.RiskScore).HasPrecision(5, 4);
            entity.Property(e => e.RiskFactors).HasColumnType("jsonb");
            entity.Property(e => e.RecommendedActions).HasColumnType("jsonb");
            entity.Property(e => e.SourceAgentId).HasMaxLength(50);
            entity.Property(e => e.ResolutionNotes).HasMaxLength(2000);
            entity.Property(e => e.EscalationReason).HasMaxLength(500);
            entity.Property(e => e.RelatedEntityIds).HasColumnType("jsonb");
            
            entity.HasOne(e => e.Tenant)
                .WithMany(t => t.Alerts)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.TransactionAnalysis)
                .WithOne(t => t.Alert)
                .HasForeignKey<FraudAlert>(e => e.TransactionAnalysisId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasQueryFilter(e => !e.IsDeleted);
        });
        
        // AuditLog configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.EntityType);
            entity.HasIndex(e => e.CreatedAt);
            
            entity.Property(e => e.Action).HasMaxLength(100).IsRequired();
            entity.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.OldValues).HasColumnType("jsonb");
            entity.Property(e => e.NewValues).HasColumnType("jsonb");
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
        });
        
        // AgentConfiguration
        modelBuilder.Entity<Core.Entities.AgentConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AgentId).IsUnique();
            
            entity.Property(e => e.AgentId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.AzureAgentId).HasMaxLength(100);
            entity.Property(e => e.ModelDeploymentName).HasMaxLength(100);
            entity.Property(e => e.Settings).HasColumnType("jsonb");
            entity.Property(e => e.AvailableTools).HasColumnType("jsonb");
            
            entity.HasQueryFilter(e => !e.IsDeleted);
        });
        
        // AgentInvocationLog
        modelBuilder.Entity<AgentInvocationLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.AgentId);
            entity.HasIndex(e => e.CreatedAt);
            
            entity.Property(e => e.AgentId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.RequestContext).HasColumnType("jsonb");
            entity.Property(e => e.ResponseSummary).HasMaxLength(2000);
            entity.Property(e => e.ConfidenceScore).HasPrecision(5, 4);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.ErrorCode).HasMaxLength(50);
            entity.Property(e => e.ToolsInvoked).HasColumnType("jsonb");
        });
        
        // AgentMessage
        modelBuilder.Entity<AgentMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MessageId);
            entity.HasIndex(e => e.CorrelationId);
            entity.HasIndex(e => e.CreatedAt);
            
            entity.Property(e => e.MessageId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SourceAgentId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.TargetAgentId).HasMaxLength(50);
            entity.Property(e => e.Context).HasColumnType("jsonb");
            entity.Property(e => e.Payload).HasColumnType("jsonb");
            entity.Property(e => e.Confidence).HasPrecision(5, 4);
            entity.Property(e => e.ReasoningChain).HasColumnType("jsonb");
            entity.Property(e => e.CorrelationId).HasMaxLength(50);
            entity.Property(e => e.ParentMessageId).HasMaxLength(50);
        });
    }
    
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }
    
    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity && (e.State == EntityState.Added || e.State == EntityState.Modified));
        
        foreach (var entry in entries)
        {
            var entity = (BaseEntity)entry.Entity;
            
            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = DateTime.UtcNow;
            }
            
            if (entry.State == EntityState.Modified)
            {
                entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
