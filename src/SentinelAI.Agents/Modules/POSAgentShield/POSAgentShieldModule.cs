using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SentinelAI.Agents.Orchestration;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Enums;
using SentinelAI.Core.Interfaces;

namespace SentinelAI.Agents.Modules.POSAgentShield;

/// <summary>
/// POS/Agent Shield Module - Specialized fraud detection for POS terminals and agent networks
/// </summary>
public class POSAgentShieldModule : IFraudModule
{
    private readonly IEnumerable<IFraudAgent> _agents;
    private readonly ILogger<POSAgentShieldModule> _logger;
    
    public ModuleType Type => ModuleType.POSAgentShield;
    public string Name => "POS/Agent Shield";
    public IEnumerable<IFraudAgent> Agents => _agents;
    
    public POSAgentShieldModule(
        TerminalMonitorAgent terminalMonitor,
        MuleHunterAgent muleHunter,
        ILogger<POSAgentShieldModule> logger)
    {
        _agents = new List<IFraudAgent> { terminalMonitor, muleHunter };
        _logger = logger;
    }
    
    public Task InitializeAsync(ModuleConfiguration config, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("POS/Agent Shield module initialized");
        return Task.CompletedTask;
    }
    
    public async Task<ModuleAnalysisResult> AnalyzeAsync(
        AnalysisContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Only analyze POS/Agent channel transactions
        if (context.Transaction.Channel != TransactionChannel.POS && 
            context.Transaction.Channel != TransactionChannel.Agent)
        {
            return new ModuleAnalysisResult
            {
                Module = Type,
                IsSuccess = true,
                RiskScore = 0,
                Confidence = 1.0m,
                Explanation = "Transaction not via POS/Agent channel - skipped",
                ProcessingTimeMs = 0
            };
        }
        
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
            _logger.LogError(ex, "POS/Agent Shield analysis failed");
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
        _logger.LogDebug("POS/Agent Shield received insight: {InsightType}", insight.InsightType);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Terminal Monitor Agent - Terminal behavior, tampering, skimming detection
/// </summary>
public class TerminalMonitorAgent : BaseFraudAgent
{
    public override string AgentId => Core.Constants.AgentIds.TerminalMonitor;
    public override string Name => "Terminal Monitor";
    public override AgentLevel Level => AgentLevel.Specialist;
    public override ModuleType Module => ModuleType.POSAgentShield;
    
    public TerminalMonitorAgent(
        IOptions<AzureAIFoundrySettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<TerminalMonitorAgent> logger) 
        : base(settings, httpClientFactory, logger)
    {
    }
    
    public override async Task<AgentResponse> ProcessAsync(
        AgentRequest request, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_settings.AgentIds.TerminalMonitor))
        {
            return await SimulateAgentProcessingAsync(request, cancellationToken);
        }
        
        var prompt = $"""
            Analyze POS terminal behavior for this transaction:
            
            Transaction: {request.Transaction.Amount} {request.Transaction.Currency}
            Channel: {request.Transaction.Channel}
            Device: {request.Transaction.DeviceFingerprint ?? "Unknown"}
            Location: ({request.Transaction.Latitude}, {request.Transaction.Longitude})
            
            Evaluate:
            1. Terminal integrity indicators
            2. Card harvesting patterns (high BIN diversity)
            3. Location consistency with registered terminal
            4. Transaction velocity for this terminal
            5. Signs of skimming or cloning
            
            Provide terminal risk assessment.
            """;
        
        return await InvokeAzureAgentAsync(
            _settings.AgentIds.TerminalMonitor,
            prompt,
            request.Context,
            cancellationToken);
    }
    
    protected override List<RiskFactor> GenerateSimulatedRiskFactors(TransactionAnalysisRequest transaction)
    {
        var factors = new List<RiskFactor>();
        
        // Terminal location mismatch
        if (transaction.Latitude.HasValue && Random.Shared.NextDouble() > 0.9)
        {
            factors.Add(new RiskFactor
            {
                Code = Core.Constants.RiskFactorCodes.TerminalLocationMismatch,
                Description = "Terminal transaction location differs from registered location",
                Weight = 0.35m,
                Contribution = 0.18m,
                Source = AgentId
            });
        }
        
        // High BIN diversity (card harvesting indicator)
        if (Random.Shared.NextDouble() > 0.95)
        {
            factors.Add(new RiskFactor
            {
                Code = Core.Constants.RiskFactorCodes.HighBinDiversityTerminal,
                Description = "Terminal showing unusually high card BIN diversity",
                Weight = 0.4m,
                Contribution = 0.2m,
                Source = AgentId
            });
        }
        
        return factors;
    }
}

/// <summary>
/// Mule Hunter Agent - Mule networks, rapid cash-out, ransom payment detection
/// </summary>
public class MuleHunterAgent : BaseFraudAgent
{
    public override string AgentId => Core.Constants.AgentIds.MuleHunter;
    public override string Name => "Mule Hunter";
    public override AgentLevel Level => AgentLevel.Specialist;
    public override ModuleType Module => ModuleType.POSAgentShield;
    
    public MuleHunterAgent(
        IOptions<AzureAIFoundrySettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<MuleHunterAgent> logger) 
        : base(settings, httpClientFactory, logger)
    {
    }
    
    public override async Task<AgentResponse> ProcessAsync(
        AgentRequest request, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_settings.AgentIds.MuleHunter))
        {
            return await SimulateAgentProcessingAsync(request, cancellationToken);
        }
        
        var prompt = $"""
            Analyze transaction for mule network indicators:
            
            Transaction: {request.Transaction.Amount} {request.Transaction.Currency}
            Channel: {request.Transaction.Channel}
            Time: {request.Transaction.TransactionTime}
            
            Evaluate for:
            1. Mule account patterns (rapid in/out, round amounts)
            2. Coordinated cash-out signatures
            3. Kidnap ransom payment patterns (specific amounts, urgency indicators)
            4. Money laundering layering patterns
            5. New account + high volume indicators
            
            Provide mule network risk assessment.
            """;
        
        return await InvokeAzureAgentAsync(
            _settings.AgentIds.MuleHunter,
            prompt,
            request.Context,
            cancellationToken);
    }
    
    protected override List<RiskFactor> GenerateSimulatedRiskFactors(TransactionAnalysisRequest transaction)
    {
        var factors = new List<RiskFactor>();
        
        // Suspicious cash-out pattern
        if (transaction.Amount >= 100000 && transaction.Amount % 10000 == 0)
        {
            factors.Add(new RiskFactor
            {
                Code = Core.Constants.RiskFactorCodes.SuspiciousCashOutPattern,
                Description = "Round amount transaction consistent with cash-out pattern",
                Weight = 0.25m,
                Contribution = 0.12m,
                Source = AgentId
            });
        }
        
        // Mule network indicator (simulated)
        if (Random.Shared.NextDouble() > 0.97)
        {
            factors.Add(new RiskFactor
            {
                Code = Core.Constants.RiskFactorCodes.MuleNetworkIndicator,
                Description = "Transaction matches known mule network patterns",
                Weight = 0.5m,
                Contribution = 0.3m,
                Source = AgentId
            });
        }
        
        return factors;
    }
}
