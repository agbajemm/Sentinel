using Bogus;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Entities;
using SentinelAI.Core.Enums;

namespace SentinelAI.Tests;

/// <summary>
/// Fake data generators for testing
/// </summary>
public static class TestDataGenerators
{
    private static readonly Faker _faker = new("en");
    
    /// <summary>
    /// Generates a fake transaction analysis request
    /// </summary>
    public static TransactionAnalysisRequest GenerateTransactionRequest(
        decimal? amount = null,
        TransactionChannel? channel = null)
    {
        var faker = new Faker<TransactionAnalysisRequest>()
            .RuleFor(t => t.TransactionId, f => f.Random.AlphaNumeric(20))
            .RuleFor(t => t.Amount, f => amount ?? f.Finance.Amount(100, 1_000_000))
            .RuleFor(t => t.Currency, f => "NGN")
            .RuleFor(t => t.Channel, f => channel ?? f.PickRandom<TransactionChannel>())
            .RuleFor(t => t.TransactionTime, f => f.Date.Recent(1))
            .RuleFor(t => t.SourceAccount, f => f.Finance.Account(10))
            .RuleFor(t => t.DestinationAccount, f => f.Finance.Account(10))
            .RuleFor(t => t.CustomerId, f => f.Random.AlphaNumeric(15))
            .RuleFor(t => t.BeneficiaryName, f => f.Name.FullName())
            .RuleFor(t => t.DeviceFingerprint, f => f.Random.Hash())
            .RuleFor(t => t.IpAddress, f => f.Internet.Ip())
            .RuleFor(t => t.SessionId, f => f.Random.Guid().ToString())
            .RuleFor(t => t.UserAgent, f => f.Internet.UserAgent())
            .RuleFor(t => t.Latitude, f => f.Address.Latitude())
            .RuleFor(t => t.Longitude, f => f.Address.Longitude())
            .RuleFor(t => t.TransactionType, f => f.PickRandom(new[] { "Transfer", "Payment", "Withdrawal" }))
            .RuleFor(t => t.Narration, f => f.Lorem.Sentence(3))
            .RuleFor(t => t.IncludeExplanation, f => true);
        
        return faker.Generate();
    }
    
    /// <summary>
    /// Generates a list of fake transaction requests
    /// </summary>
    public static List<TransactionAnalysisRequest> GenerateTransactionRequests(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => GenerateTransactionRequest())
            .ToList();
    }
    
    /// <summary>
    /// Generates a fake tenant
    /// </summary>
    public static Tenant GenerateTenant(SubscriptionTier? tier = null)
    {
        var faker = new Faker<Tenant>()
            .RuleFor(t => t.Id, f => f.Random.Guid())
            .RuleFor(t => t.Name, f => f.Company.CompanyName())
            .RuleFor(t => t.Code, f => f.Random.AlphaNumeric(10).ToUpperInvariant())
            .RuleFor(t => t.Description, f => f.Lorem.Sentence())
            .RuleFor(t => t.SubscriptionTier, f => tier ?? f.PickRandom<SubscriptionTier>())
            .RuleFor(t => t.IsActive, f => true)
            .RuleFor(t => t.EnabledModules, f => new List<ModuleType>
            {
                ModuleType.TransactionSentinel,
                ModuleType.POSAgentShield
            })
            .RuleFor(t => t.LowRiskThreshold, f => 0.3m)
            .RuleFor(t => t.MediumRiskThreshold, f => 0.5m)
            .RuleFor(t => t.HighRiskThreshold, f => 0.7m)
            .RuleFor(t => t.CriticalRiskThreshold, f => 0.9m)
            .RuleFor(t => t.RequestsPerMinute, f => 1000)
            .RuleFor(t => t.RequestsPerDay, f => 100000)
            .RuleFor(t => t.CreatedAt, f => f.Date.Past(1));
        
        return faker.Generate();
    }
    
    /// <summary>
    /// Generates a fake create tenant request
    /// </summary>
    public static CreateTenantRequest GenerateCreateTenantRequest()
    {
        var faker = new Faker<CreateTenantRequest>()
            .RuleFor(t => t.Name, f => f.Company.CompanyName())
            .RuleFor(t => t.Code, f => f.Random.AlphaNumeric(10).ToUpperInvariant())
            .RuleFor(t => t.Description, f => f.Lorem.Sentence())
            .RuleFor(t => t.SubscriptionTier, f => f.PickRandom<SubscriptionTier>())
            .RuleFor(t => t.WebhookUrl, f => f.Internet.Url().Replace("http://", "https://"))
            .RuleFor(t => t.ContactEmail, f => f.Internet.Email())
            .RuleFor(t => t.ContactPhone, f => "+234" + f.Random.Number(8000000000, 9999999999));
        
        return faker.Generate();
    }
    
    /// <summary>
    /// Generates a fake fraud alert
    /// </summary>
    public static FraudAlert GenerateFraudAlert(
        Guid? tenantId = null,
        RiskLevel? riskLevel = null,
        AlertStatus? status = null)
    {
        var faker = new Faker<FraudAlert>()
            .RuleFor(a => a.Id, f => f.Random.Guid())
            .RuleFor(a => a.TenantId, f => tenantId ?? f.Random.Guid())
            .RuleFor(a => a.AlertReference, f => $"ALT-{DateTime.UtcNow:yyyyMMdd}-{f.Random.AlphaNumeric(8).ToUpperInvariant()}")
            .RuleFor(a => a.Status, f => status ?? f.PickRandom<AlertStatus>())
            .RuleFor(a => a.RiskLevel, f => riskLevel ?? f.PickRandom<RiskLevel>())
            .RuleFor(a => a.RiskScore, f => f.Random.Decimal(0.5m, 1.0m))
            .RuleFor(a => a.Title, f => f.Lorem.Sentence(5))
            .RuleFor(a => a.Description, f => f.Lorem.Paragraph())
            .RuleFor(a => a.SourceModule, f => f.PickRandom<ModuleType>())
            .RuleFor(a => a.CreatedAt, f => f.Date.Recent(7));
        
        return faker.Generate();
    }
    
    /// <summary>
    /// Generates fake risk factors
    /// </summary>
    public static List<RiskFactor> GenerateRiskFactors(int count = 3)
    {
        var codes = new[]
        {
            ("RF001", "High transaction amount for customer profile"),
            ("RF002", "Transaction at unusual time"),
            ("RF003", "Velocity threshold exceeded"),
            ("RF004", "First-time beneficiary"),
            ("RF005", "High-risk destination country"),
            ("RF006", "Geographic anomaly detected"),
            ("RF007", "Device fingerprint changed")
        };
        
        return _faker.Random.Shuffle(codes)
            .Take(count)
            .Select((c, i) => new RiskFactor
            {
                Code = c.Item1,
                Description = c.Item2,
                Weight = _faker.Random.Decimal(0.1m, 0.5m),
                Contribution = _faker.Random.Decimal(0.05m, 0.3m),
                Source = _faker.PickRandom(new[] { "TransactionSentinel", "BehaviorProfiler", "RiskScorer" })
            })
            .ToList();
    }
    
    /// <summary>
    /// Generates a fake fraud analysis response
    /// </summary>
    public static FraudAnalysisResponse GenerateFraudAnalysisResponse(
        RiskLevel? riskLevel = null,
        decimal? riskScore = null)
    {
        var level = riskLevel ?? _faker.PickRandom<RiskLevel>();
        var score = riskScore ?? level switch
        {
            RiskLevel.Critical => _faker.Random.Decimal(0.9m, 1.0m),
            RiskLevel.High => _faker.Random.Decimal(0.7m, 0.89m),
            RiskLevel.Medium => _faker.Random.Decimal(0.5m, 0.69m),
            _ => _faker.Random.Decimal(0.0m, 0.49m)
        };
        
        return new FraudAnalysisResponse
        {
            TransactionId = _faker.Random.AlphaNumeric(20),
            AnalysisId = _faker.Random.Guid().ToString(),
            RiskScore = score,
            RiskLevel = level,
            RecommendedAction = level switch
            {
                RiskLevel.Critical => RecommendedAction.Block,
                RiskLevel.High => RecommendedAction.Review,
                RiskLevel.Medium => RecommendedAction.Challenge,
                _ => RecommendedAction.Approve
            },
            RiskFactors = GenerateRiskFactors(),
            Explanation = $"Transaction assessed as {level} risk with score {score:P0}",
            ProcessingTimeMs = _faker.Random.Int(50, 500),
            AnalyzedAt = DateTime.UtcNow,
            ModulesInvoked = new List<string> { "TransactionSentinel", "POSAgentShield" },
            AgentsInvoked = new List<string> { "AGT-TXN-001", "AGT-TXN-002", "AGT-POS-001" }
        };
    }
    
    /// <summary>
    /// Generates a fake orchestrator response
    /// </summary>
    public static OrchestratorResponse GenerateOrchestratorResponse(
        bool isSuccess = true,
        decimal? riskScore = null)
    {
        var score = riskScore ?? _faker.Random.Decimal(0.0m, 1.0m);
        var level = score switch
        {
            >= 0.9m => RiskLevel.Critical,
            >= 0.7m => RiskLevel.High,
            >= 0.5m => RiskLevel.Medium,
            _ => RiskLevel.Low
        };
        
        return new OrchestratorResponse
        {
            CorrelationId = _faker.Random.Guid().ToString("N"),
            IsSuccess = isSuccess,
            AggregatedRiskScore = score,
            RiskLevel = level,
            RecommendedAction = level switch
            {
                RiskLevel.Critical => RecommendedAction.Block,
                RiskLevel.High => RecommendedAction.Review,
                RiskLevel.Medium => RecommendedAction.Challenge,
                _ => RecommendedAction.Approve
            },
            AgentResponses = new List<AgentResponse>
            {
                new()
                {
                    AgentId = "AGT-TXN-001",
                    IsSuccess = true,
                    RiskScore = score,
                    Confidence = _faker.Random.Decimal(0.7m, 1.0m),
                    RiskFactors = GenerateRiskFactors(2),
                    ProcessingTimeMs = _faker.Random.Int(20, 100)
                }
            },
            AggregatedRiskFactors = GenerateRiskFactors(),
            Explanation = $"Analysis complete. Risk level: {level}",
            TotalProcessingTimeMs = _faker.Random.Int(100, 500)
        };
    }
}
