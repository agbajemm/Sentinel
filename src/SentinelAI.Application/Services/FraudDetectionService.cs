using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Entities;
using SentinelAI.Core.Enums;
using SentinelAI.Core.Exceptions;
using SentinelAI.Core.Interfaces;

namespace SentinelAI.Application.Services;

/// <summary>
/// Main fraud detection service implementation
/// </summary>
public class FraudDetectionService : IFraudDetectionService
{
    private readonly IAgentOrchestrator _orchestrator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEncryptionService _encryptionService;
    private readonly IAlertService _alertService;
    private readonly ILogger<FraudDetectionService> _logger;
    
    public FraudDetectionService(
        IAgentOrchestrator orchestrator,
        IUnitOfWork unitOfWork,
        IEncryptionService encryptionService,
        IAlertService alertService,
        ILogger<FraudDetectionService> logger)
    {
        _orchestrator = orchestrator;
        _unitOfWork = unitOfWork;
        _encryptionService = encryptionService;
        _alertService = alertService;
        _logger = logger;
    }
    
    public async Task<FraudAnalysisResponse> AnalyzeTransactionAsync(
        TransactionAnalysisRequest request,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = Guid.NewGuid().ToString("N");
        
        _logger.LogInformation(
            "Starting fraud analysis for transaction {TransactionId}, TenantId: {TenantId}, CorrelationId: {CorrelationId}",
            request.TransactionId, tenantId, correlationId);
        
        try
        {
            // Get tenant configuration
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId, cancellationToken)
                ?? throw new EntityNotFoundException("Tenant", tenantId);
            
            // Orchestrate agent analysis
            var orchestratorRequest = new OrchestratorRequest
            {
                TenantId = tenantId,
                Transaction = request,
                RequiredModules = request.ModulesToInvoke,
                CorrelationId = correlationId
            };
            
            var orchestratorResponse = await _orchestrator.OrchestrateAsync(orchestratorRequest, cancellationToken);
            
            // Determine risk level based on tenant thresholds
            var riskLevel = DetermineRiskLevel(orchestratorResponse.AggregatedRiskScore, tenant);
            var recommendedAction = DetermineAction(riskLevel, orchestratorResponse);
            
            // Create transaction analysis record
            var analysisRecord = await CreateAnalysisRecordAsync(
                tenantId, request, orchestratorResponse, riskLevel, recommendedAction, 
                (int)stopwatch.ElapsedMilliseconds, cancellationToken);
            
            // Create alert if necessary
            FraudAlert? alert = null;
            if (riskLevel >= RiskLevel.High)
            {
                alert = await CreateAlertIfNeededAsync(tenantId, analysisRecord, orchestratorResponse, cancellationToken);
            }
            
            stopwatch.Stop();
            
            var response = new FraudAnalysisResponse
            {
                TransactionId = request.TransactionId,
                AnalysisId = analysisRecord.Id.ToString(),
                RiskScore = orchestratorResponse.AggregatedRiskScore,
                RiskLevel = riskLevel,
                RecommendedAction = recommendedAction,
                RiskFactors = orchestratorResponse.AggregatedRiskFactors,
                Explanation = orchestratorResponse.Explanation,
                ModuleResults = orchestratorResponse.AgentResponses
                    .GroupBy(r => GetModuleFromAgentId(r.AgentId))
                    .Select(g => new ModuleResult
                    {
                        Module = g.Key,
                        RiskScore = g.Max(r => r.RiskScore),
                        Confidence = g.Average(r => r.Confidence),
                        RiskFactors = g.SelectMany(r => r.RiskFactors).ToList(),
                        ProcessingTimeMs = g.Sum(r => r.ProcessingTimeMs),
                        Explanation = string.Join(" ", g.Select(r => r.Explanation).Where(e => !string.IsNullOrEmpty(e)))
                    }).ToList(),
                ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds,
                AnalyzedAt = DateTime.UtcNow,
                ModulesInvoked = orchestratorResponse.AgentResponses.Select(r => GetModuleFromAgentId(r.AgentId).ToString()).Distinct().ToList(),
                AgentsInvoked = orchestratorResponse.AgentResponses.Select(r => r.AgentId).ToList(),
                AlertId = alert?.Id,
                AlertReference = alert?.AlertReference
            };
            
            _logger.LogInformation(
                "Completed fraud analysis for transaction {TransactionId}. Risk: {RiskLevel}, Score: {RiskScore}, Time: {ProcessingTime}ms",
                request.TransactionId, riskLevel, orchestratorResponse.AggregatedRiskScore, stopwatch.ElapsedMilliseconds);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error analyzing transaction {TransactionId}, CorrelationId: {CorrelationId}",
                request.TransactionId, correlationId);
            throw;
        }
    }
    
    public async Task<IEnumerable<FraudAnalysisResponse>> AnalyzeBatchAsync(
        IEnumerable<TransactionAnalysisRequest> requests,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<FraudAnalysisResponse>();
        
        // Process in parallel with limited concurrency
        var semaphore = new SemaphoreSlim(10); // Max 10 concurrent analyses
        var tasks = requests.Select(async request =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await AnalyzeTransactionAsync(request, tenantId, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        results.AddRange(await Task.WhenAll(tasks));
        return results;
    }
    
    public async Task<IEnumerable<FraudAnalysisResponse>> GetCustomerHistoryAsync(
        Guid tenantId,
        string customerIdHash,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var analyses = await _unitOfWork.TransactionAnalyses.FindAsync(
            a => a.TenantId == tenantId && a.SourceAccountHash == customerIdHash,
            cancellationToken);
        
        return analyses
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .Select(a => new FraudAnalysisResponse
            {
                TransactionId = a.ExternalTransactionId,
                AnalysisId = a.Id.ToString(),
                RiskScore = a.RiskScore,
                RiskLevel = a.RiskLevel,
                RecommendedAction = a.RecommendedAction,
                RiskFactors = !string.IsNullOrEmpty(a.RiskFactors) 
                    ? JsonSerializer.Deserialize<List<RiskFactor>>(a.RiskFactors) ?? new()
                    : new(),
                Explanation = a.Explanation,
                ProcessingTimeMs = a.ProcessingTimeMs,
                AnalyzedAt = a.CreatedAt
            });
    }
    
    private async Task<TransactionAnalysis> CreateAnalysisRecordAsync(
        Guid tenantId,
        TransactionAnalysisRequest request,
        OrchestratorResponse response,
        RiskLevel riskLevel,
        RecommendedAction action,
        int processingTimeMs,
        CancellationToken cancellationToken)
    {
        var record = new TransactionAnalysis
        {
            TenantId = tenantId,
            TransactionReference = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            ExternalTransactionId = request.TransactionId,
            Amount = request.Amount,
            Currency = request.Currency,
            Channel = request.Channel,
            TransactionTime = request.TransactionTime,
            EncryptedSourceAccount = _encryptionService.EncryptForTenant(request.SourceAccount, tenantId),
            EncryptedDestinationAccount = _encryptionService.EncryptForTenant(request.DestinationAccount, tenantId),
            EncryptedCustomerId = !string.IsNullOrEmpty(request.CustomerId) 
                ? _encryptionService.EncryptForTenant(request.CustomerId, tenantId) 
                : null,
            SourceAccountHash = _encryptionService.Hash(request.SourceAccount),
            DestinationAccountHash = _encryptionService.Hash(request.DestinationAccount),
            DeviceFingerprint = request.DeviceFingerprint,
            IpAddress = request.IpAddress,
            SessionId = request.SessionId,
            UserAgent = request.UserAgent,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            RiskScore = response.AggregatedRiskScore,
            RiskLevel = riskLevel,
            RecommendedAction = action,
            RiskFactors = JsonSerializer.Serialize(response.AggregatedRiskFactors),
            Explanation = response.Explanation,
            ProcessingTimeMs = processingTimeMs,
            ModulesInvoked = JsonSerializer.Serialize(
                response.AgentResponses.Select(r => GetModuleFromAgentId(r.AgentId)).Distinct()),
            AgentsInvoked = JsonSerializer.Serialize(response.AgentResponses.Select(r => r.AgentId))
        };
        
        await _unitOfWork.TransactionAnalyses.AddAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return record;
    }
    
    private async Task<FraudAlert?> CreateAlertIfNeededAsync(
        Guid tenantId,
        TransactionAnalysis analysis,
        OrchestratorResponse response,
        CancellationToken cancellationToken)
    {
        var createRequest = new CreateAlertRequest
        {
            TenantId = tenantId,
            TransactionAnalysisId = analysis.Id,
            Title = $"High Risk Transaction Detected - {analysis.RiskLevel}",
            Description = response.Explanation ?? $"Transaction {analysis.ExternalTransactionId} flagged with risk score {analysis.RiskScore:P0}",
            RiskLevel = analysis.RiskLevel,
            RiskScore = analysis.RiskScore,
            SourceModule = GetPrimaryModule(response),
            RiskFactors = response.AggregatedRiskFactors.Select(rf => rf.Description).ToList(),
            RecommendedActions = GetRecommendedActions(analysis.RiskLevel)
        };
        
        var alertDto = await _alertService.CreateAlertAsync(createRequest, cancellationToken);
        
        // Get the actual entity
        var alert = await _unitOfWork.FraudAlerts.GetByIdAsync(alertDto.Id, cancellationToken);
        return alert;
    }
    
    private static RiskLevel DetermineRiskLevel(decimal riskScore, Tenant tenant)
    {
        if (riskScore >= tenant.CriticalRiskThreshold) return RiskLevel.Critical;
        if (riskScore >= tenant.HighRiskThreshold) return RiskLevel.High;
        if (riskScore >= tenant.MediumRiskThreshold) return RiskLevel.Medium;
        return RiskLevel.Low;
    }
    
    private static RecommendedAction DetermineAction(RiskLevel level, OrchestratorResponse response)
    {
        return level switch
        {
            RiskLevel.Critical => RecommendedAction.Block,
            RiskLevel.High => RecommendedAction.Review,
            RiskLevel.Medium => RecommendedAction.Challenge,
            _ => RecommendedAction.Approve
        };
    }
    
    private static ModuleType GetModuleFromAgentId(string agentId)
    {
        return agentId switch
        {
            var id when id.StartsWith("AGT-TXN") => ModuleType.TransactionSentinel,
            var id when id.StartsWith("AGT-POS") => ModuleType.POSAgentShield,
            var id when id.StartsWith("AGT-IDN") => ModuleType.IdentityFortress,
            var id when id.StartsWith("AGT-INS") => ModuleType.InsiderThreat,
            var id when id.StartsWith("AGT-NET") => ModuleType.NetworkIntelligence,
            var id when id.StartsWith("AGT-INV") => ModuleType.InvestigationAssistant,
            _ => ModuleType.TransactionSentinel
        };
    }
    
    private static ModuleType GetPrimaryModule(OrchestratorResponse response)
    {
        var highestScoreAgent = response.AgentResponses
            .OrderByDescending(r => r.RiskScore)
            .FirstOrDefault();
        
        return highestScoreAgent != null 
            ? GetModuleFromAgentId(highestScoreAgent.AgentId) 
            : ModuleType.TransactionSentinel;
    }
    
    private static List<string> GetRecommendedActions(RiskLevel level) => level switch
    {
        RiskLevel.Critical => new() { "Block transaction immediately", "Escalate to fraud team", "Contact customer" },
        RiskLevel.High => new() { "Hold for manual review", "Request additional authentication", "Monitor related transactions" },
        RiskLevel.Medium => new() { "Apply step-up authentication", "Log for review", "Monitor customer activity" },
        _ => new() { "Continue monitoring" }
    };
}
