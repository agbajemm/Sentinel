using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SentinelAI.Agents.Orchestration;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Enums;
using SentinelAI.Core.Interfaces;

namespace SentinelAI.Agents.Modules.TransactionSentinel;

/// <summary>
/// Transaction Sentinel Module - Real-time transaction monitoring
/// </summary>
public class TransactionSentinelModule : IFraudModule
{
    private readonly IEnumerable<IFraudAgent> _agents;
    private readonly ILogger<TransactionSentinelModule> _logger;
    private ModuleConfiguration? _config;
    
    public ModuleType Type => ModuleType.TransactionSentinel;
    public string Name => "Transaction Sentinel";
    public IEnumerable<IFraudAgent> Agents => _agents;
    
    public TransactionSentinelModule(
        TransactionAnalyzerAgent transactionAnalyzer,
        BehaviorProfilerAgent behaviorProfiler,
        RiskScorerAgent riskScorer,
        ILogger<TransactionSentinelModule> logger)
    {
        _agents = new List<IFraudAgent> { transactionAnalyzer, behaviorProfiler, riskScorer };
        _logger = logger;
    }
    
    public Task InitializeAsync(ModuleConfiguration config, CancellationToken cancellationToken = default)
    {
        _config = config;
        _logger.LogInformation("Transaction Sentinel module initialized");
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
        
        _logger.LogDebug("Transaction Sentinel analyzing transaction {TransactionId}", 
            context.Transaction.TransactionId);
        
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
            _logger.LogError(ex, "Transaction Sentinel analysis failed");
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
        _logger.LogDebug("Transaction Sentinel received insight: {InsightType}", insight.InsightType);
        // Handle cross-module insights (e.g., mule network detection from POS module)
        return Task.CompletedTask;
    }
}

/// <summary>
/// Transaction Analyzer Agent - Pattern analysis, velocity, amount anomalies
/// </summary>
public class TransactionAnalyzerAgent : BaseFraudAgent
{
    public override string AgentId => Core.Constants.AgentIds.TransactionAnalyzer;
    public override string Name => "Transaction Analyzer";
    public override AgentLevel Level => AgentLevel.Specialist;
    public override ModuleType Module => ModuleType.TransactionSentinel;
    
    public TransactionAnalyzerAgent(
        IOptions<AzureAIFoundrySettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<TransactionAnalyzerAgent> logger) 
        : base(settings, httpClientFactory, logger)
    {
    }
    
    public override async Task<AgentResponse> ProcessAsync(
        AgentRequest request, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Transaction Analyzer processing request {RequestId}", request.RequestId);
        
        // Check if Azure AI Foundry is configured
        if (string.IsNullOrEmpty(_settings.AgentIds.TransactionAnalyzer))
        {
            // Use simulation mode
            return await SimulateAgentProcessingAsync(request, cancellationToken);
        }
        
        // Build prompt for Azure AI agent
        var prompt = BuildAnalysisPrompt(request.Transaction);
        
        return await InvokeAzureAgentAsync(
            _settings.AgentIds.TransactionAnalyzer,
            prompt,
            request.Context,
            cancellationToken);
    }
    
    protected override List<RiskFactor> GenerateSimulatedRiskFactors(TransactionAnalysisRequest transaction)
    {
        var factors = base.GenerateSimulatedRiskFactors(transaction);
        
        // Velocity check simulation
        if (transaction.Amount > 500000)
        {
            factors.Add(new RiskFactor
            {
                Code = Core.Constants.RiskFactorCodes.VelocityThresholdExceeded,
                Description = "Multiple high-value transactions detected in short timeframe",
                Weight = 0.25m,
                Contribution = 0.12m,
                Source = AgentId
            });
        }
        
        // First-time recipient check
        if (Random.Shared.NextDouble() > 0.7)
        {
            factors.Add(new RiskFactor
            {
                Code = Core.Constants.RiskFactorCodes.FirstTimeRecipient,
                Description = "First transaction to this beneficiary",
                Weight = 0.15m,
                Contribution = 0.08m,
                Source = AgentId
            });
        }
        
        return factors;
    }
    
    private static string BuildAnalysisPrompt(TransactionAnalysisRequest transaction)
    {
        return $"""
            Analyze the following transaction for fraud indicators:
            
            Transaction Details:
            - Amount: {transaction.Amount} {transaction.Currency}
            - Channel: {transaction.Channel}
            - Time: {transaction.TransactionTime:yyyy-MM-dd HH:mm:ss} UTC
            - Source Account: [MASKED]
            - Destination Account: [MASKED]
            
            Device/Session:
            - Device Fingerprint: {transaction.DeviceFingerprint ?? "Not provided"}
            - IP Address: {transaction.IpAddress ?? "Not provided"}
            - User Agent: {transaction.UserAgent ?? "Not provided"}
            
            Analyze for:
            1. Amount anomalies compared to typical patterns
            2. Velocity patterns (frequency of transactions)
            3. Time-of-day anomalies
            4. Channel switching patterns
            
            Provide risk score (0-1), confidence (0-1), risk factors, and explanation.
            """;
    }
}

/// <summary>
/// Behavior Profiler Agent - Customer behavior profiles and deviation detection
/// </summary>
public class BehaviorProfilerAgent : BaseFraudAgent
{
    public override string AgentId => Core.Constants.AgentIds.BehaviorProfiler;
    public override string Name => "Behavior Profiler";
    public override AgentLevel Level => AgentLevel.Specialist;
    public override ModuleType Module => ModuleType.TransactionSentinel;
    
    public BehaviorProfilerAgent(
        IOptions<AzureAIFoundrySettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<BehaviorProfilerAgent> logger) 
        : base(settings, httpClientFactory, logger)
    {
    }
    
    public override async Task<AgentResponse> ProcessAsync(
        AgentRequest request, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_settings.AgentIds.BehaviorProfiler))
        {
            return await SimulateAgentProcessingAsync(request, cancellationToken);
        }
        
        var prompt = $"""
            Analyze customer behavior for this transaction:
            
            Transaction: {request.Transaction.Amount} {request.Transaction.Currency} via {request.Transaction.Channel}
            
            Evaluate:
            1. Deviation from established behavior patterns
            2. Session anomalies (mouse movements, typing patterns if available)
            3. Navigation patterns
            4. Geographic behavior consistency
            
            Provide behavioral risk assessment.
            """;
        
        return await InvokeAzureAgentAsync(
            _settings.AgentIds.BehaviorProfiler,
            prompt,
            request.Context,
            cancellationToken);
    }
    
    protected override List<RiskFactor> GenerateSimulatedRiskFactors(TransactionAnalysisRequest transaction)
    {
        var factors = new List<RiskFactor>();
        
        // Device change simulation
        if (!string.IsNullOrEmpty(transaction.DeviceFingerprint) && Random.Shared.NextDouble() > 0.8)
        {
            factors.Add(new RiskFactor
            {
                Code = Core.Constants.RiskFactorCodes.DeviceChanged,
                Description = "Transaction from previously unseen device",
                Weight = 0.2m,
                Contribution = 0.1m,
                Source = AgentId
            });
        }
        
        // Session anomaly simulation
        if (Random.Shared.NextDouble() > 0.85)
        {
            factors.Add(new RiskFactor
            {
                Code = Core.Constants.RiskFactorCodes.SessionAnomaly,
                Description = "Session behavior deviates from customer profile",
                Weight = 0.15m,
                Contribution = 0.08m,
                Source = AgentId
            });
        }
        
        return factors;
    }
}

/// <summary>
/// Risk Scorer Agent - Composite risk scoring with action thresholds
/// </summary>
public class RiskScorerAgent : BaseFraudAgent
{
    public override string AgentId => Core.Constants.AgentIds.RiskScorer;
    public override string Name => "Risk Scorer";
    public override AgentLevel Level => AgentLevel.Specialist;
    public override ModuleType Module => ModuleType.TransactionSentinel;
    
    public RiskScorerAgent(
        IOptions<AzureAIFoundrySettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<RiskScorerAgent> logger) 
        : base(settings, httpClientFactory, logger)
    {
    }
    
    public override async Task<AgentResponse> ProcessAsync(
        AgentRequest request, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_settings.AgentIds.RiskScorer))
        {
            return await SimulateAgentProcessingAsync(request, cancellationToken);
        }
        
        var prompt = $"""
            Calculate composite risk score for transaction:
            
            Amount: {request.Transaction.Amount} {request.Transaction.Currency}
            Channel: {request.Transaction.Channel}
            Time: {request.Transaction.TransactionTime}
            
            Consider all available signals and provide:
            1. Final composite risk score
            2. Confidence level
            3. Key contributing factors
            4. Recommended action threshold
            """;
        
        return await InvokeAzureAgentAsync(
            _settings.AgentIds.RiskScorer,
            prompt,
            request.Context,
            cancellationToken);
    }
    
    protected override decimal CalculateSimulatedRiskScore(List<RiskFactor> factors)
    {
        // More sophisticated scoring for the risk scorer
        if (!factors.Any()) return 0.05m;
        
        var baseScore = factors.Sum(f => f.Contribution);
        var weightedScore = factors.Sum(f => f.Weight * f.Contribution);
        
        return Math.Min((baseScore + weightedScore) / 2, 1.0m);
    }
}
