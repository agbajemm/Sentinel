using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Enums;
using SentinelAI.Infrastructure.Data;
using Xunit;

namespace SentinelAI.Tests.Integration;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    
    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<SentinelDbContext>));
                
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                
                // Add in-memory database for testing
                services.AddDbContext<SentinelDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDatabase_" + Guid.NewGuid());
                });
            });
        });
        
        _client = _factory.CreateClient();
    }
    
    [Fact]
    public async Task HealthCheck_ShouldReturnHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health/live");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    
    [Fact]
    public async Task RootEndpoint_ShouldReturnApiInfo()
    {
        // Act
        var response = await _client.GetAsync("/");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<dynamic>();
        Assert.NotNull(content);
    }
    
    [Fact]
    public async Task Analysis_WithoutAuth_ShouldReturn401()
    {
        // Arrange
        var request = TestDataGenerators.GenerateTransactionRequest();
        
        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/analysis/transaction", request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task Alerts_WithoutAuth_ShouldReturn401()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/alerts");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

/// <summary>
/// Test fixture for authenticated API tests
/// </summary>
public class AuthenticatedApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    
    public AuthenticatedApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<SentinelDbContext>));
                
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                
                // Add in-memory database for testing
                services.AddDbContext<SentinelDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDatabase_" + Guid.NewGuid());
                });
            });
        });
    }
    
    // Additional authenticated tests would go here
    // These would require setting up test authentication
}

/// <summary>
/// Tests for the data masking service
/// </summary>
public class DataMaskingServiceTests
{
    private readonly SentinelAI.Infrastructure.Security.DataMaskingService _maskingService;
    
    public DataMaskingServiceTests()
    {
        _maskingService = new SentinelAI.Infrastructure.Security.DataMaskingService();
    }
    
    [Theory]
    [InlineData("1234567890", "****7890")]
    [InlineData("0123456789", "****6789")]
    [InlineData("123", "****")]
    public void MaskAccountNumber_ShouldMaskCorrectly(string input, string expected)
    {
        // Act
        var result = _maskingService.MaskAccountNumber(input);
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("+2348012345678", "***5678")]
    [InlineData("08012345678", "***5678")]
    public void MaskPhoneNumber_ShouldMaskCorrectly(string input, string expected)
    {
        // Act
        var result = _maskingService.MaskPhoneNumber(input);
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("test@example.com", "t***t@***.com")]
    [InlineData("john.doe@company.co.uk", "j***e@***.uk")]
    public void MaskEmail_ShouldMaskCorrectly(string input, string expected)
    {
        // Act
        var result = _maskingService.MaskEmail(input);
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("12345678901", "*******8901")]
    public void MaskBvn_ShouldMaskCorrectly(string input, string expected)
    {
        // Act
        var result = _maskingService.MaskBvn(input);
        
        // Assert
        result.Should().Be(expected);
    }
}

/// <summary>
/// Performance tests for critical paths
/// </summary>
public class PerformanceTests
{
    [Fact]
    public void TransactionRequestGeneration_ShouldBePerformant()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Act
        var requests = TestDataGenerators.GenerateTransactionRequests(1000);
        
        stopwatch.Stop();
        
        // Assert
        requests.Should().HaveCount(1000);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Generating 1000 requests should take less than 1 second");
    }
    
    [Fact]
    public void RiskFactorGeneration_ShouldBeConsistent()
    {
        // Act
        var riskFactors1 = TestDataGenerators.GenerateRiskFactors(5);
        var riskFactors2 = TestDataGenerators.GenerateRiskFactors(5);
        
        // Assert
        riskFactors1.Should().HaveCount(5);
        riskFactors2.Should().HaveCount(5);
        
        // Verify uniqueness of codes within each set
        riskFactors1.Select(rf => rf.Code).Should().OnlyHaveUniqueItems();
        riskFactors2.Select(rf => rf.Code).Should().OnlyHaveUniqueItems();
    }
}
