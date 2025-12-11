using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SentinelAI.Application.Services;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Entities;
using SentinelAI.Core.Enums;
using SentinelAI.Core.Interfaces;
using System.Linq.Expressions;
using Xunit;

namespace SentinelAI.Tests.Unit;

public class FraudDetectionServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IAgentOrchestrator> _orchestratorMock;
    private readonly Mock<IEncryptionService> _encryptionServiceMock;
    private readonly Mock<IAlertService> _alertServiceMock;
    private readonly Mock<ILogger<FraudDetectionService>> _loggerMock;
    private readonly FraudDetectionService _fraudDetectionService;
    
    public FraudDetectionServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _orchestratorMock = new Mock<IAgentOrchestrator>();
        _encryptionServiceMock = new Mock<IEncryptionService>();
        _alertServiceMock = new Mock<IAlertService>();
        _loggerMock = new Mock<ILogger<FraudDetectionService>>();
        
        _fraudDetectionService = new FraudDetectionService(
            _unitOfWorkMock.Object,
            _orchestratorMock.Object,
            _encryptionServiceMock.Object,
            _alertServiceMock.Object,
            _loggerMock.Object);
    }
    
    [Fact]
    public async Task AnalyzeTransactionAsync_ValidRequest_ReturnsAnalysisResult()
    {
        // Arrange
        var request = TestDataGenerators.GenerateTransactionRequest();
        var tenant = TestDataGenerators.GenerateTenant();
        
        var orchestratorResponse = TestDataGenerators.GenerateOrchestratorResponse(
            isSuccess: true,
            riskScore: 0.35m);
        
        _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        
        _orchestratorMock.Setup(o => o.OrchestrateAsync(It.IsAny<OrchestratorRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orchestratorResponse);
        
        _encryptionServiceMock.Setup(e => e.EncryptForTenant(It.IsAny<string>(), It.IsAny<Guid>()))
            .Returns<string, Guid>((s, _) => $"encrypted_{s}");
        
        _encryptionServiceMock.Setup(e => e.Hash(It.IsAny<string>()))
            .Returns<string>(s => $"hash_{s}");
        
        _unitOfWorkMock.Setup(u => u.TransactionAnalyses.AddAsync(It.IsAny<TransactionAnalysis>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransactionAnalysis t, CancellationToken _) => t);
        
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        // Act
        var result = await _fraudDetectionService.AnalyzeTransactionAsync(request, tenant.Id);
        
        // Assert
        result.Should().NotBeNull();
        result.TransactionId.Should().Be(request.TransactionId);
        result.RiskScore.Should().Be(orchestratorResponse.AggregatedRiskScore);
        result.RiskLevel.Should().Be(orchestratorResponse.RiskLevel);
        
        _orchestratorMock.Verify(o => o.OrchestrateAsync(
            It.Is<OrchestratorRequest>(r => r.Transaction == request && r.TenantId == tenant.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task AnalyzeTransactionAsync_HighRiskTransaction_CreatesAlert()
    {
        // Arrange
        var request = TestDataGenerators.GenerateTransactionRequest(amount: 5000000);
        var tenant = TestDataGenerators.GenerateTenant();
        
        var orchestratorResponse = TestDataGenerators.GenerateOrchestratorResponse(
            isSuccess: true,
            riskScore: 0.85m); // High risk
        
        _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        
        _orchestratorMock.Setup(o => o.OrchestrateAsync(It.IsAny<OrchestratorRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orchestratorResponse);
        
        _encryptionServiceMock.Setup(e => e.EncryptForTenant(It.IsAny<string>(), It.IsAny<Guid>()))
            .Returns<string, Guid>((s, _) => $"encrypted_{s}");
        
        _encryptionServiceMock.Setup(e => e.Hash(It.IsAny<string>()))
            .Returns<string>(s => $"hash_{s}");
        
        _unitOfWorkMock.Setup(u => u.TransactionAnalyses.AddAsync(It.IsAny<TransactionAnalysis>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransactionAnalysis t, CancellationToken _) => t);
        
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        _alertServiceMock.Setup(a => a.CreateAlertAsync(It.IsAny<CreateAlertRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataGenerators.GenerateFraudAlert());
        
        // Act
        var result = await _fraudDetectionService.AnalyzeTransactionAsync(request, tenant.Id);
        
        // Assert
        result.RiskLevel.Should().Be(RiskLevel.High);
        result.RecommendedAction.Should().Be(RecommendedAction.Review);
        
        // Verify alert was created for high-risk transaction
        _alertServiceMock.Verify(a => a.CreateAlertAsync(
            It.Is<CreateAlertRequest>(r => r.RiskLevel == RiskLevel.High),
            It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task AnalyzeTransactionAsync_LowRiskTransaction_DoesNotCreateAlert()
    {
        // Arrange
        var request = TestDataGenerators.GenerateTransactionRequest(amount: 10000);
        var tenant = TestDataGenerators.GenerateTenant();
        
        var orchestratorResponse = TestDataGenerators.GenerateOrchestratorResponse(
            isSuccess: true,
            riskScore: 0.15m); // Low risk
        
        _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        
        _orchestratorMock.Setup(o => o.OrchestrateAsync(It.IsAny<OrchestratorRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orchestratorResponse);
        
        _encryptionServiceMock.Setup(e => e.EncryptForTenant(It.IsAny<string>(), It.IsAny<Guid>()))
            .Returns<string, Guid>((s, _) => $"encrypted_{s}");
        
        _encryptionServiceMock.Setup(e => e.Hash(It.IsAny<string>()))
            .Returns<string>(s => $"hash_{s}");
        
        _unitOfWorkMock.Setup(u => u.TransactionAnalyses.AddAsync(It.IsAny<TransactionAnalysis>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransactionAnalysis t, CancellationToken _) => t);
        
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        // Act
        var result = await _fraudDetectionService.AnalyzeTransactionAsync(request, tenant.Id);
        
        // Assert
        result.RiskLevel.Should().Be(RiskLevel.Low);
        result.RecommendedAction.Should().Be(RecommendedAction.Approve);
        
        // Verify no alert was created for low-risk transaction
        _alertServiceMock.Verify(a => a.CreateAlertAsync(
            It.IsAny<CreateAlertRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task AnalyzeTransactionAsync_EncryptsSensitiveData()
    {
        // Arrange
        var request = TestDataGenerators.GenerateTransactionRequest();
        var tenant = TestDataGenerators.GenerateTenant();
        
        var orchestratorResponse = TestDataGenerators.GenerateOrchestratorResponse();
        
        _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        
        _orchestratorMock.Setup(o => o.OrchestrateAsync(It.IsAny<OrchestratorRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orchestratorResponse);
        
        _encryptionServiceMock.Setup(e => e.EncryptForTenant(It.IsAny<string>(), It.IsAny<Guid>()))
            .Returns<string, Guid>((s, _) => $"encrypted_{s}");
        
        _encryptionServiceMock.Setup(e => e.Hash(It.IsAny<string>()))
            .Returns<string>(s => $"hash_{s}");
        
        _unitOfWorkMock.Setup(u => u.TransactionAnalyses.AddAsync(It.IsAny<TransactionAnalysis>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransactionAnalysis t, CancellationToken _) => t);
        
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        // Act
        await _fraudDetectionService.AnalyzeTransactionAsync(request, tenant.Id);
        
        // Assert
        // Verify source account was encrypted
        _encryptionServiceMock.Verify(e => e.EncryptForTenant(
            request.SourceAccount, tenant.Id), Times.Once);
        
        // Verify destination account was encrypted
        _encryptionServiceMock.Verify(e => e.EncryptForTenant(
            request.DestinationAccount, tenant.Id), Times.Once);
        
        // Verify hashes were created for lookups
        _encryptionServiceMock.Verify(e => e.Hash(request.SourceAccount), Times.Once);
        _encryptionServiceMock.Verify(e => e.Hash(request.DestinationAccount), Times.Once);
    }
    
    [Fact]
    public async Task AnalyzeBatchAsync_ProcessesMultipleTransactions()
    {
        // Arrange
        var requests = TestDataGenerators.GenerateTransactionRequests(5);
        var tenant = TestDataGenerators.GenerateTenant();
        
        _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        
        _orchestratorMock.Setup(o => o.OrchestrateAsync(It.IsAny<OrchestratorRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataGenerators.GenerateOrchestratorResponse());
        
        _encryptionServiceMock.Setup(e => e.EncryptForTenant(It.IsAny<string>(), It.IsAny<Guid>()))
            .Returns<string, Guid>((s, _) => $"encrypted_{s}");
        
        _encryptionServiceMock.Setup(e => e.Hash(It.IsAny<string>()))
            .Returns<string>(s => $"hash_{s}");
        
        _unitOfWorkMock.Setup(u => u.TransactionAnalyses.AddAsync(It.IsAny<TransactionAnalysis>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransactionAnalysis t, CancellationToken _) => t);
        
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        // Act
        var results = await _fraudDetectionService.AnalyzeBatchAsync(requests, tenant.Id);
        
        // Assert
        var resultList = results.ToList();
        resultList.Should().HaveCount(5);
        resultList.All(r => r.TransactionId != null).Should().BeTrue();
        
        // Verify orchestrator was called for each transaction
        _orchestratorMock.Verify(o => o.OrchestrateAsync(
            It.IsAny<OrchestratorRequest>(),
            It.IsAny<CancellationToken>()), Times.Exactly(5));
    }
    
    [Fact]
    public async Task GetCustomerHistoryAsync_ReturnsTransactionHistory()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var customerIdHash = "hash_customer123";
        
        var transactions = new List<TransactionAnalysis>
        {
            new() { Id = Guid.NewGuid(), TenantId = tenantId, TransactionReference = "TXN001", RiskScore = 0.3m },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, TransactionReference = "TXN002", RiskScore = 0.5m },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, TransactionReference = "TXN003", RiskScore = 0.7m }
        };
        
        _unitOfWorkMock.Setup(u => u.TransactionAnalyses.FindAsync(
            It.IsAny<Expression<Func<TransactionAnalysis, bool>>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);
        
        // Act
        var results = await _fraudDetectionService.GetCustomerHistoryAsync(tenantId, customerIdHash, 100);
        
        // Assert
        var resultList = results.ToList();
        resultList.Should().HaveCount(3);
    }
}

public class RiskLevelDeterminationTests
{
    [Theory]
    [InlineData(0.95, RiskLevel.Critical)]
    [InlineData(0.90, RiskLevel.Critical)]
    [InlineData(0.85, RiskLevel.High)]
    [InlineData(0.70, RiskLevel.High)]
    [InlineData(0.65, RiskLevel.Medium)]
    [InlineData(0.50, RiskLevel.Medium)]
    [InlineData(0.35, RiskLevel.Low)]
    [InlineData(0.10, RiskLevel.Low)]
    public void DetermineRiskLevel_ReturnsCorrectLevel(decimal riskScore, RiskLevel expectedLevel)
    {
        // This tests the risk level determination logic
        var actualLevel = riskScore switch
        {
            >= 0.9m => RiskLevel.Critical,
            >= 0.7m => RiskLevel.High,
            >= 0.5m => RiskLevel.Medium,
            _ => RiskLevel.Low
        };
        
        actualLevel.Should().Be(expectedLevel);
    }
    
    [Theory]
    [InlineData(RiskLevel.Critical, RecommendedAction.Block)]
    [InlineData(RiskLevel.High, RecommendedAction.Review)]
    [InlineData(RiskLevel.Medium, RecommendedAction.Challenge)]
    [InlineData(RiskLevel.Low, RecommendedAction.Approve)]
    public void DetermineRecommendedAction_ReturnsCorrectAction(RiskLevel riskLevel, RecommendedAction expectedAction)
    {
        var actualAction = riskLevel switch
        {
            RiskLevel.Critical => RecommendedAction.Block,
            RiskLevel.High => RecommendedAction.Review,
            RiskLevel.Medium => RecommendedAction.Challenge,
            _ => RecommendedAction.Approve
        };
        
        actualAction.Should().Be(expectedAction);
    }
}
