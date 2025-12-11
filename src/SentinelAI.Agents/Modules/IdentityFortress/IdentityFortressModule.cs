using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SentinelAI.Agents.Orchestration;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Enums;
using SentinelAI.Core.Interfaces;

namespace SentinelAI.Agents.Modules.IdentityFortress;

/// <summary>
/// Identity Fortress Module - Identity verification, synthetic identity detection, BVN/NIN intelligence
/// </summary>
public class IdentityFortressModule : IFraudModule
{
    private readonly IEnumerable<IFraudAgent> _agents;
    private readonly ILogger<IdentityFortressModule> _logger;
    
    public ModuleType Type => ModuleType.IdentityFortress;
    public string Name => "Identity Fortress";
    public IEnumerable<IFraudAgent> Agents => _agents;
    
    public IdentityFortressModule(
        SyntheticDetectorAgent syntheticDetector,
        ILogger<IdentityFortressModule> logger)
    {
        _agents = new List<IFraudAgent> { syntheticDetector };
        _logger = logger;
    }
    
    public Task InitializeAsync(ModuleConfiguration config, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Identity Fortress module initialized");
        return Task.CompletedTask;
    }
    
    public async Task<ModuleAnalysisResult> AnalyzeAsync(
        AnalysisContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var allRiskFactors = new List<RiskFactor>();
        var explanations = new List<string>();
        decimal maxRiskScore = 0;
        decimal totalConfidence = 0;
        int agentCount = 0;
        
        try
        {
            foreach (var agent in _agents)
            {
                var agentRequest = new AgentRequest
                {
                    RequestId = Guid.NewGuid().ToString("N"),
                    TenantId = context.TenantId,
                    Transaction = context.Transaction,
                    Context = context.SharedContext,
                    CorrelationId = context.CorrelationId
                };
                
                var response = await agent.ProcessAsync(agentRequest, cancellationToken);
                
                if (response.IsSuccess)
                {
                    allRiskFactors.AddRange(response.RiskFactors);
                    if (!string.IsNullOrEmpty(response.Explanation))
                        explanations.Add(response.Explanation);
                    
                    maxRiskScore = Math.Max(maxRiskScore, response.RiskScore);
                    totalConfidence += response.Confidence;
                    agentCount++;
                }
            }
            
            stopwatch.Stop();
            
            return new ModuleAnalysisResult
            {
                Module = Type,
                IsSuccess = true,
                RiskScore = maxRiskScore,
                Confidence = agentCount > 0 ? totalConfidence / agentCount : 0,
                RiskFactors = allRiskFactors,
                Explanation = string.Join(" ", explanations),
                ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Identity Fortress analysis failed");
            return new ModuleAnalysisResult
            {
                Module = Type,
                IsSuccess = false,
                ErrorMessage = ex.Message,
                ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }
    
    public Task HandleInsightAsync(AgentInsight insight, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Identity Fortress received insight: {InsightType}", insight.InsightType);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Synthetic Detector Agent - Synthetic/manufactured identity detection via graph analysis
/// </summary>
public class SyntheticDetectorAgent : BaseFraudAgent
{
    public override string AgentId => Core.Constants.AgentIds.SyntheticDetector;
    public override string Name => "Synthetic Identity Detector";
    public override AgentLevel Level => AgentLevel.Specialist;
    public override ModuleType Module => ModuleType.IdentityFortress;
    
    public SyntheticDetectorAgent(
        IOptions<AzureAIFoundrySettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<SyntheticDetectorAgent> logger) 
        : base(settings, httpClientFactory, logger)
    {
    }
    
    public override async Task<AgentResponse> ProcessAsync(
        AgentRequest request, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_settings.AgentIds.SyntheticDetector))
        {
            return await SimulateAgentProcessingAsync(request, cancellationToken);
        }
        
        var prompt = $"""
            Analyze identity indicators for synthetic identity risk:
            
            Transaction: {request.Transaction.Amount} {request.Transaction.Currency}
            Customer ID: [MASKED]
            Device: {request.Transaction.DeviceFingerprint ?? "Unknown"}
            
            Evaluate for:
            1. Synthetic identity patterns (mismatched data points)
            2. Credit nurturing behavior (slow buildup before bust-out)
            3. Thin-file risk indicators
            4. Device linked to multiple identities
            5. BVN/NIN graph anomalies
            6. Recent SIM swap on associated phone number
            
            Provide synthetic identity risk assessment.
            """;
        
        return await InvokeAzureAgentAsync(
            _settings.AgentIds.SyntheticDetector,
            prompt,
            request.Context,
            cancellationToken);
    }
    
    protected override List<RiskFactor> GenerateSimulatedRiskFactors(TransactionAnalysisRequest transaction)
    {
        var factors = new List<RiskFactor>();
        
        // Synthetic identity indicator (simulated)
        if (Random.Shared.NextDouble() > 0.95)
        {
            factors.Add(new RiskFactor
            {
                Code = Core.Constants.RiskFactorCodes.SyntheticIdentityIndicator,
                Description = "Identity data points show inconsistencies typical of synthetic identities",
                Weight = 0.45m,
                Contribution = 0.25m,
                Source = AgentId
            });
        }
        
        // Device linked to multiple accounts
        if (!string.IsNullOrEmpty(transaction.DeviceFingerprint) && Random.Shared.NextDouble() > 0.9)
        {
            factors.Add(new RiskFactor
            {
                Code = Core.Constants.RiskFactorCodes.DeviceLinkedToMultipleAccounts,
                Description = "Device fingerprint associated with multiple customer accounts",
                Weight = 0.3m,
                Contribution = 0.15m,
                Source = AgentId
            });
        }
        
        // Recent SIM swap
        if (Random.Shared.NextDouble() > 0.92)
        {
            factors.Add(new RiskFactor
            {
                Code = Core.Constants.RiskFactorCodes.RecentSimSwap,
                Description = "Recent SIM swap detected on associated phone number",
                Weight = 0.35m,
                Contribution = 0.18m,
                Source = AgentId
            });
        }
        
        return factors;
    }
}
