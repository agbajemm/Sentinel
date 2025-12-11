using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SentinelAI.Application.Services;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Entities;
using SentinelAI.Core.Enums;
using SentinelAI.Core.Exceptions;
using SentinelAI.Core.Interfaces;
using System.Linq.Expressions;
using Xunit;

namespace SentinelAI.Tests.Unit;

public class TenantServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IEncryptionService> _encryptionServiceMock;
    private readonly Mock<IApiKeyService> _apiKeyServiceMock;
    private readonly Mock<ILogger<TenantService>> _loggerMock;
    private readonly TenantService _tenantService;
    
    public TenantServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _encryptionServiceMock = new Mock<IEncryptionService>();
        _apiKeyServiceMock = new Mock<IApiKeyService>();
        _loggerMock = new Mock<ILogger<TenantService>>();
        
        _tenantService = new TenantService(
            _unitOfWorkMock.Object,
            _encryptionServiceMock.Object,
            _apiKeyServiceMock.Object,
            _loggerMock.Object);
    }
    
    [Fact]
    public async Task CreateTenantAsync_ValidRequest_ShouldCreateTenant()
    {
        // Arrange
        var request = TestDataGenerators.GenerateCreateTenantRequest();
        var apiKey = "sk_live_testkey123";
        var apiKeyHash = "hashedkey123";
        
        _unitOfWorkMock.Setup(u => u.Tenants.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<Tenant, bool>>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);
        
        _apiKeyServiceMock.Setup(a => a.GenerateApiKey())
            .Returns((apiKey, apiKeyHash));
        
        _encryptionServiceMock.Setup(e => e.Encrypt(It.IsAny<string>()))
            .Returns<string>(s => $"encrypted_{s}");
        
        _unitOfWorkMock.Setup(u => u.Tenants.AddAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);
        
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        // Act
        var result = await _tenantService.CreateTenantAsync(request);
        
        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(request.Name);
        result.Code.Should().Be(request.Code.ToUpperInvariant());
        result.SubscriptionTier.Should().Be(request.SubscriptionTier);
        result.IsActive.Should().BeTrue();
        
        _unitOfWorkMock.Verify(u => u.Tenants.AddAsync(
            It.Is<Tenant>(t => t.Name == request.Name),
            It.IsAny<CancellationToken>()), Times.Once);
        
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task CreateTenantAsync_DuplicateCode_ShouldThrowConflictException()
    {
        // Arrange
        var request = TestDataGenerators.GenerateCreateTenantRequest();
        var existingTenant = TestDataGenerators.GenerateTenant();
        existingTenant.Code = request.Code;
        
        _unitOfWorkMock.Setup(u => u.Tenants.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<Tenant, bool>>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTenant);
        
        // Act & Assert
        var action = () => _tenantService.CreateTenantAsync(request);
        await action.Should().ThrowAsync<ConflictException>()
            .WithMessage($"Tenant with code '{request.Code}' already exists.");
    }
    
    [Fact]
    public async Task GetTenantAsync_ExistingTenant_ShouldReturnTenant()
    {
        // Arrange
        var tenant = TestDataGenerators.GenerateTenant();
        
        _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        
        // Act
        var result = await _tenantService.GetTenantAsync(tenant.Id);
        
        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(tenant.Id);
        result.Name.Should().Be(tenant.Name);
        result.Code.Should().Be(tenant.Code);
    }
    
    [Fact]
    public async Task GetTenantAsync_NonExistingTenant_ShouldReturnNull()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        
        _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);
        
        // Act
        var result = await _tenantService.GetTenantAsync(tenantId);
        
        // Assert
        result.Should().BeNull();
    }
    
    [Fact]
    public async Task UpdateTenantAsync_ValidRequest_ShouldUpdateTenant()
    {
        // Arrange
        var tenant = TestDataGenerators.GenerateTenant();
        var updateRequest = new UpdateTenantRequest
        {
            Name = "Updated Name",
            Description = "Updated Description",
            LowRiskThreshold = 0.25m
        };
        
        _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        // Act
        var result = await _tenantService.UpdateTenantAsync(tenant.Id, updateRequest);
        
        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(updateRequest.Name);
        
        _unitOfWorkMock.Verify(u => u.Tenants.UpdateAsync(
            It.Is<Tenant>(t => t.Name == updateRequest.Name),
            It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task UpdateTenantAsync_NonExistingTenant_ShouldThrowNotFoundException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var updateRequest = new UpdateTenantRequest { Name = "New Name" };
        
        _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);
        
        // Act & Assert
        var action = () => _tenantService.UpdateTenantAsync(tenantId, updateRequest);
        await action.Should().ThrowAsync<EntityNotFoundException>();
    }
    
    [Fact]
    public async Task EnableModuleAsync_ShouldAddModuleToTenant()
    {
        // Arrange
        var tenant = TestDataGenerators.GenerateTenant();
        tenant.EnabledModules = new List<ModuleType> { ModuleType.TransactionSentinel };
        var newModule = ModuleType.IdentityFortress;
        
        _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        // Act
        await _tenantService.EnableModuleAsync(tenant.Id, newModule);
        
        // Assert
        tenant.EnabledModules.Should().Contain(newModule);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task DisableModuleAsync_ShouldRemoveModuleFromTenant()
    {
        // Arrange
        var tenant = TestDataGenerators.GenerateTenant();
        var moduleToRemove = ModuleType.POSAgentShield;
        tenant.EnabledModules = new List<ModuleType> { ModuleType.TransactionSentinel, moduleToRemove };
        
        _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        // Act
        await _tenantService.DisableModuleAsync(tenant.Id, moduleToRemove);
        
        // Assert
        tenant.EnabledModules.Should().NotContain(moduleToRemove);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class AlertServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<AlertService>> _loggerMock;
    private readonly AlertService _alertService;
    
    public AlertServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<AlertService>>();
        
        _alertService = new AlertService(_unitOfWorkMock.Object, _loggerMock.Object);
    }
    
    [Fact]
    public async Task CreateAlertAsync_ValidRequest_ShouldCreateAlert()
    {
        // Arrange
        var request = new CreateAlertRequest
        {
            TenantId = Guid.NewGuid(),
            Title = "Test Alert",
            Description = "Test Description",
            RiskLevel = RiskLevel.High,
            RiskScore = 0.85m,
            SourceModule = ModuleType.TransactionSentinel,
            RiskFactors = new List<string> { "High amount", "New beneficiary" }
        };
        
        _unitOfWorkMock.Setup(u => u.FraudAlerts.AddAsync(It.IsAny<FraudAlert>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FraudAlert a, CancellationToken _) => a);
        
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        // Act
        var result = await _alertService.CreateAlertAsync(request);
        
        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(request.Title);
        result.RiskLevel.Should().Be(request.RiskLevel);
        result.Status.Should().Be(AlertStatus.New);
        result.AlertReference.Should().StartWith("ALT-");
    }
    
    [Fact]
    public async Task GetAlertAsync_ExistingAlert_ShouldReturnAlert()
    {
        // Arrange
        var alert = TestDataGenerators.GenerateFraudAlert();
        
        _unitOfWorkMock.Setup(u => u.FraudAlerts.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<FraudAlert, bool>>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);
        
        // Act
        var result = await _alertService.GetAlertAsync(alert.TenantId, alert.Id);
        
        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(alert.Id);
        result.AlertReference.Should().Be(alert.AlertReference);
    }
    
    [Fact]
    public async Task UpdateAlertStatusAsync_ValidRequest_ShouldUpdateStatus()
    {
        // Arrange
        var alert = TestDataGenerators.GenerateFraudAlert(status: AlertStatus.New);
        var request = new UpdateAlertStatusRequest
        {
            Status = AlertStatus.Resolved,
            Notes = "False positive - verified with customer"
        };
        
        _unitOfWorkMock.Setup(u => u.FraudAlerts.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<FraudAlert, bool>>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);
        
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        // Act
        var result = await _alertService.UpdateAlertStatusAsync(alert.TenantId, alert.Id, request);
        
        // Assert
        result.Status.Should().Be(AlertStatus.Resolved);
        alert.ResolvedAt.Should().NotBeNull();
        alert.ResolutionNotes.Should().Be(request.Notes);
    }
    
    [Fact]
    public async Task EscalateAlertAsync_ShouldMarkAlertAsEscalated()
    {
        // Arrange
        var alert = TestDataGenerators.GenerateFraudAlert(status: AlertStatus.Investigating);
        var escalationReason = "Requires senior analyst review";
        
        _unitOfWorkMock.Setup(u => u.FraudAlerts.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<FraudAlert, bool>>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);
        
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        // Act
        await _alertService.EscalateAlertAsync(alert.TenantId, alert.Id, escalationReason);
        
        // Assert
        alert.IsEscalated.Should().BeTrue();
        alert.EscalatedAt.Should().NotBeNull();
        alert.EscalationReason.Should().Be(escalationReason);
        alert.Status.Should().Be(AlertStatus.Escalated);
    }
}
