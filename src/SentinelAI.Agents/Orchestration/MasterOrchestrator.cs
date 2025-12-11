using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Enums;
using SentinelAI.Core.Interfaces;

namespace SentinelAI.Agents.Orchestration;

/// <summary>
/// Configuration for Azure AI Foundry agents
/// </summary>
public class AzureAIFoundrySettings
{
    public const string SectionName = "AzureAIFoundry";
    
    public required string Endpoint { get; set; }
    public required string ProjectName { get; set; }
    public required string ApiKey { get; set; }
    public string ApiVersion { get; set; } = "2024-07-01-preview";
    public int DefaultTimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
    
    // Agent IDs for each module
    public AgentIdSettings AgentIds { get; set; } = new();
}

public class AgentIdSettings
{
    // Orchestration
    public string MasterOrchestrator { get; set; } = "";
    
    // Transaction Sentinel
    public string TransactionAnalyzer { get; set; } = "";
    public string BehaviorProfiler { get; set; } = "";
    public string RiskScorer { get; set; } = "";
    
    // POS/Agent Shield
    public string TerminalMonitor { get; set; } = "";
    public string AgentProfiler { get; set; } = "";
    public string MuleHunter { get; set; } = "";
    public string GeoValidator { get; set; } = "";
    
    // Identity Fortress
    public string IdentityVerifier { get; set; } = "";
    public string SyntheticDetector { get; set; } = "";
    public string GraphAnalyzer { get; set; } = "";
    
    // Insider Threat
    public string EmployeeBehaviorAnalyzer { get; set; } = "";
    public string ControlBypassDetector { get; set; } = "";
    
    // Network Intelligence
    public string GraphBuilder { get; set; } = "";
    public string NetworkAnalyzer { get; set; } = "";
    
    // Investigation Assistant
    public string CaseBuilder { get; set; } = "";
    public string ReportGenerator { get; set; } = "";
}

/// <summary>
/// Master orchestrator that coordinates all fraud detection agents
/// </summary>
public class MasterOrchestrator : IAgentOrchestrator
{
    private readonly IEnumerable<IFraudModule> _modules;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MasterOrchestrator> _logger;
    private readonly AzureAIFoundrySettings _settings;
    
    public MasterOrchestrator(
        IEnumerable<IFraudModule> modules,
        IUnitOfWork unitOfWork,
        IOptions<AzureAIFoundrySettings> settings,
        ILogger<MasterOrchestrator> logger)
    {
        _modules = modules;
        _unitOfWork = unitOfWork;
        _settings = settings.Value;
        _logger = logger;
    }
    
    public async Task<OrchestratorResponse> OrchestrateAsync(
        OrchestratorRequest request,
        CancellationToken cancellationToken = default)
    {
        var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();
        var agentResponses = new List<AgentResponse>();
        var allRiskFactors = new List<RiskFactor>();
        
        _logger.LogInformation(
            "Orchestrator starting analysis. CorrelationId: {CorrelationId}, TenantId: {TenantId}",
            correlationId, request.TenantId);
        
        try
        {
            // Get enabled modules for tenant
            var enabledModules = await GetEnabledModulesForTenantAsync(request.TenantId, cancellationToken);
            
            // Filter to requested modules if specified
            var modulesToInvoke = request.RequiredModules?.Any() == true
                ? enabledModules.Where(m => request.RequiredModules.Contains(m))
                : enabledModules;
            
            // Create analysis context
            var context = new AnalysisContext
            {
                CorrelationId = correlationId,
                TenantId = request.TenantId,
                Transaction = request.Transaction,
                SharedContext = request.Context ?? new Dictionary<string, object>()
            };
            
            // Execute modules in parallel with controlled concurrency
            var moduleResults = await ExecuteModulesAsync(modulesToInvoke, context, cancellationToken);
            
            // Aggregate results
            foreach (var result in moduleResults)
            {
                if (result.IsSuccess)
                {
                    allRiskFactors.AddRange(result.RiskFactors);
                    agentResponses.Add(new AgentResponse
                    {
                        AgentId = $"MODULE-{result.Module}",
                        IsSuccess = true,
                        RiskScore = result.RiskScore,
                        Confidence = result.Confidence,
                        RiskFactors = result.RiskFactors,
                        Explanation = result.Explanation,
                        ProcessingTimeMs = result.ProcessingTimeMs
                    });
                }
            }
            
            // Calculate aggregated risk score using weighted average
            var aggregatedScore = CalculateAggregatedRiskScore(moduleResults);
            var riskLevel = DetermineRiskLevel(aggregatedScore);
            var recommendedAction = DetermineRecommendedAction(riskLevel, allRiskFactors);
            
            // Generate explanation
            var explanation = GenerateExplanation(moduleResults, allRiskFactors, aggregatedScore);
            
            stopwatch.Stop();
            
            _logger.LogInformation(
                "Orchestrator completed. CorrelationId: {CorrelationId}, RiskScore: {RiskScore}, Time: {Time}ms",
                correlationId, aggregatedScore, stopwatch.ElapsedMilliseconds);
            
            return new OrchestratorResponse
            {
                CorrelationId = correlationId,
                IsSuccess = true,
                AggregatedRiskScore = aggregatedScore,
                RiskLevel = riskLevel,
                RecommendedAction = recommendedAction,
                AgentResponses = agentResponses,
                AggregatedRiskFactors = DeduplicateAndRankRiskFactors(allRiskFactors),
                Explanation = explanation,
                TotalProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orchestrator failed. CorrelationId: {CorrelationId}", correlationId);
            
            return new OrchestratorResponse
            {
                CorrelationId = correlationId,
                IsSuccess = false,
                ErrorMessage = "Analysis failed due to an internal error.",
                TotalProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }
    
    public async Task BroadcastInsightAsync(AgentInsight insight, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Broadcasting insight {InsightType} from {SourceAgent}",
            insight.InsightType, insight.SourceAgentId);
        
        var tasks = _modules
            .Where(m => m.Type != insight.SourceModule)
            .Select(m => m.HandleInsightAsync(insight, cancellationToken));
        
        await Task.WhenAll(tasks);
    }
    
    public async Task<IEnumerable<IFraudAgent>> GetEnabledAgentsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var enabledModules = await GetEnabledModulesForTenantAsync(tenantId, cancellationToken);
        
        return _modules
            .Where(m => enabledModules.Contains(m.Type))
            .SelectMany(m => m.Agents);
    }
    
    public Task<IEnumerable<IFraudAgent>> GetAgentsByModuleAsync(
        ModuleType module,
        CancellationToken cancellationToken = default)
    {
        var targetModule = _modules.FirstOrDefault(m => m.Type == module);
        return Task.FromResult(targetModule?.Agents ?? Enumerable.Empty<IFraudAgent>());
    }
    
    private async Task<IEnumerable<ModuleType>> GetEnabledModulesForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId, cancellationToken);
        return tenant?.EnabledModules ?? new List<ModuleType> { ModuleType.TransactionSentinel };
    }
    
    private async Task<List<ModuleAnalysisResult>> ExecuteModulesAsync(
        IEnumerable<ModuleType> moduleTypes,
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        var results = new List<ModuleAnalysisResult>();
        var semaphore = new SemaphoreSlim(5); // Max 5 concurrent modules
        
        var tasks = moduleTypes.Select(async moduleType =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var module = _modules.FirstOrDefault(m => m.Type == moduleType);
                if (module == null)
                {
                    _logger.LogWarning("Module {ModuleType} not found", moduleType);
                    return null;
                }
                
                return await module.AnalyzeAsync(context, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Module {ModuleType} failed", moduleType);
                return new ModuleAnalysisResult
                {
                    Module = moduleType,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        var completedResults = await Task.WhenAll(tasks);
        results.AddRange(completedResults.Where(r => r != null)!);
        
        return results;
    }
    
    private static decimal CalculateAggregatedRiskScore(List<ModuleAnalysisResult> results)
    {
        if (!results.Any(r => r.IsSuccess))
            return 0;
        
        var successfulResults = results.Where(r => r.IsSuccess).ToList();
        
        // Weighted by confidence
        var totalWeight = successfulResults.Sum(r => r.Confidence);
        if (totalWeight == 0) return 0;
        
        var weightedSum = successfulResults.Sum(r => r.RiskScore * r.Confidence);
        var weightedAverage = weightedSum / totalWeight;
        
        // Apply max boost if any module has critical findings
        var maxScore = successfulResults.Max(r => r.RiskScore);
        if (maxScore > 0.9m)
        {
            // Boost the score if any module found critical risk
            weightedAverage = Math.Max(weightedAverage, maxScore * 0.9m);
        }
        
        return Math.Min(weightedAverage, 1.0m);
    }
    
    private static RiskLevel DetermineRiskLevel(decimal score) => score switch
    {
        >= 0.9m => RiskLevel.Critical,
        >= 0.7m => RiskLevel.High,
        >= 0.5m => RiskLevel.Medium,
        _ => RiskLevel.Low
    };
    
    private static RecommendedAction DetermineRecommendedAction(RiskLevel level, List<RiskFactor> factors)
    {
        // Check for specific high-priority risk factors
        var hasCriticalFactor = factors.Any(f => 
            f.Code == Core.Constants.RiskFactorCodes.MuleNetworkIndicator ||
            f.Code == Core.Constants.RiskFactorCodes.SyntheticIdentityIndicator);
        
        if (hasCriticalFactor)
            return RecommendedAction.Block;
        
        return level switch
        {
            RiskLevel.Critical => RecommendedAction.Block,
            RiskLevel.High => RecommendedAction.Review,
            RiskLevel.Medium => RecommendedAction.Challenge,
            _ => RecommendedAction.Approve
        };
    }
    
    private static List<RiskFactor> DeduplicateAndRankRiskFactors(List<RiskFactor> factors)
    {
        return factors
            .GroupBy(f => f.Code)
            .Select(g => new RiskFactor
            {
                Code = g.Key,
                Description = g.First().Description,
                Weight = g.Max(f => f.Weight),
                Contribution = g.Sum(f => f.Contribution),
                Source = string.Join(", ", g.Select(f => f.Source).Distinct())
            })
            .OrderByDescending(f => f.Contribution)
            .Take(10)
            .ToList();
    }
    
    private static string GenerateExplanation(
        List<ModuleAnalysisResult> results,
        List<RiskFactor> factors,
        decimal aggregatedScore)
    {
        var topFactors = factors.OrderByDescending(f => f.Contribution).Take(3).ToList();
        
        if (!topFactors.Any())
            return "No significant risk factors detected.";
        
        var explanations = topFactors.Select(f => f.Description);
        var moduleNames = results.Where(r => r.IsSuccess).Select(r => r.Module.ToString());
        
        return $"Risk score: {aggregatedScore:P0}. Key factors: {string.Join("; ", explanations)}. " +
               $"Analysis performed by: {string.Join(", ", moduleNames)}.";
    }
}
