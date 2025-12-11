using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SentinelAI.Infrastructure.Data;

namespace SentinelAI.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory for integration testing
/// </summary>
public class SentinelAIWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"SentinelAI_Test_{Guid.NewGuid()}";
    
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            services.RemoveAll(typeof(DbContextOptions<SentinelDbContext>));
            services.RemoveAll(typeof(SentinelDbContext));
            
            // Add in-memory database for testing
            services.AddDbContext<SentinelDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });
            
            // Build service provider
            var sp = services.BuildServiceProvider();
            
            // Create database and seed test data
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            db.Database.EnsureCreated();
            
            // Seed test data
            SeedTestData(db);
        });
        
        builder.UseEnvironment("Development");
    }
    
    private static void SeedTestData(SentinelDbContext db)
    {
        // Seed a test tenant
        var tenant = new Core.Entities.Tenant
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Test Bank",
            Code = "TESTBANK",
            Description = "Test bank for integration testing",
            SubscriptionTier = Core.Enums.SubscriptionTier.Enterprise,
            IsActive = true,
            EnabledModules = new List<Core.Enums.ModuleType>
            {
                Core.Enums.ModuleType.TransactionSentinel,
                Core.Enums.ModuleType.POSAgentShield,
                Core.Enums.ModuleType.IdentityFortress
            },
            ApiKeyHash = "test_api_key_hash",
            LowRiskThreshold = 0.3m,
            MediumRiskThreshold = 0.5m,
            HighRiskThreshold = 0.7m,
            CriticalRiskThreshold = 0.9m,
            RequestsPerMinute = 1000,
            RequestsPerDay = 100000,
            CreatedAt = DateTime.UtcNow
        };
        
        db.Tenants.Add(tenant);
        
        // Seed a test user (admin)
        var adminUser = new Core.Entities.TenantUser
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            TenantId = tenant.Id,
            Email = "admin@testbank.com",
            FirstName = "Test",
            LastName = "Admin",
            PasswordHash = HashPassword("TestPassword123!"),
            Role = Core.Constants.Roles.TenantAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        
        db.TenantUsers.Add(adminUser);
        
        // Seed a test fraud analyst user
        var analystUser = new Core.Entities.TenantUser
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            TenantId = tenant.Id,
            Email = "analyst@testbank.com",
            FirstName = "Test",
            LastName = "Analyst",
            PasswordHash = HashPassword("TestPassword123!"),
            Role = Core.Constants.Roles.FraudAnalyst,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        
        db.TenantUsers.Add(analystUser);
        
        // Seed some test alerts
        for (int i = 1; i <= 5; i++)
        {
            var alert = new Core.Entities.FraudAlert
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                AlertReference = $"ALT-TEST-{i:D8}",
                Status = i % 2 == 0 ? Core.Enums.AlertStatus.New : Core.Enums.AlertStatus.Investigating,
                RiskLevel = (Core.Enums.RiskLevel)(i % 4),
                RiskScore = 0.3m + (0.15m * i),
                Title = $"Test Alert {i}",
                Description = $"This is test alert number {i}",
                SourceModule = Core.Enums.ModuleType.TransactionSentinel,
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            };
            
            db.FraudAlerts.Add(alert);
        }
        
        db.SaveChanges();
    }
    
    private static string HashPassword(string password)
    {
        // Simple hash for testing - in production use PBKDF2
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password + "test_salt");
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
    
    /// <summary>
    /// Gets an authenticated HTTP client with JWT token
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string role = "TenantAdmin")
    {
        var client = CreateClient();
        
        // Generate a test JWT token
        var token = GenerateTestToken(role);
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        
        return client;
    }
    
    /// <summary>
    /// Gets an HTTP client with API key authentication
    /// </summary>
    public HttpClient CreateApiKeyAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "sk_test_key_for_testing");
        return client;
    }
    
    private static string GenerateTestToken(string role)
    {
        // For testing purposes, create a simple JWT token
        // In production, this should use the actual TokenService
        var claims = new Dictionary<string, object>
        {
            { "sub", "22222222-2222-2222-2222-222222222222" },
            { "email", "admin@testbank.com" },
            { "user_id", "22222222-2222-2222-2222-222222222222" },
            { "tenant_id", "11111111-1111-1111-1111-111111111111" },
            { "role", role }
        };
        
        // Simple base64 encoding for test token (not secure - for testing only)
        var header = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            """{"alg":"HS256","typ":"JWT"}"""));
        var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(claims)));
        var signature = "test_signature";
        
        return $"{header}.{payload}.{signature}";
    }
}

/// <summary>
/// Test constants for integration tests
/// </summary>
public static class TestConstants
{
    public static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid TestAdminUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid TestAnalystUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    
    public const string TestAdminEmail = "admin@testbank.com";
    public const string TestAnalystEmail = "analyst@testbank.com";
    public const string TestPassword = "TestPassword123!";
    public const string TestApiKey = "sk_test_key_for_testing";
}
