using SentinelAI.Core.DTOs;
using SentinelAI.Core.Enums;

namespace SentinelAI.Core.Interfaces;

/// <summary>
/// Main fraud detection orchestration service
/// </summary>
public interface IFraudDetectionService
{
    /// <summary>
    /// Analyzes a transaction for fraud
    /// </summary>
    Task<FraudAnalysisResponse> AnalyzeTransactionAsync(
        TransactionAnalysisRequest request,
        Guid tenantId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Batch analysis of multiple transactions
    /// </summary>
    Task<IEnumerable<FraudAnalysisResponse>> AnalyzeBatchAsync(
        IEnumerable<TransactionAnalysisRequest> requests,
        Guid tenantId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets analysis history for a customer
    /// </summary>
    Task<IEnumerable<FraudAnalysisResponse>> GetCustomerHistoryAsync(
        Guid tenantId,
        string customerIdHash,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for individual fraud detection agents
/// </summary>
public interface IFraudAgent
{
    string AgentId { get; }
    string Name { get; }
    AgentLevel Level { get; }
    ModuleType Module { get; }
    
    /// <summary>
    /// Processes a fraud detection request
    /// </summary>
    Task<AgentResponse> ProcessAsync(AgentRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the tools available to this agent
    /// </summary>
    Task<IEnumerable<AgentTool>> GetToolsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Health check for the agent
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Agent orchestrator for coordinating multiple agents
/// </summary>
public interface IAgentOrchestrator
{
    /// <summary>
    /// Routes a request to appropriate agents based on context
    /// </summary>
    Task<OrchestratorResponse> OrchestrateAsync(
        OrchestratorRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Broadcasts an insight to relevant agents
    /// </summary>
    Task BroadcastInsightAsync(AgentInsight insight, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets enabled agents for a tenant
    /// </summary>
    Task<IEnumerable<IFraudAgent>> GetEnabledAgentsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets agents by module
    /// </summary>
    Task<IEnumerable<IFraudAgent>> GetAgentsByModuleAsync(
        ModuleType module,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for fraud detection modules
/// </summary>
public interface IFraudModule
{
    ModuleType Type { get; }
    string Name { get; }
    IEnumerable<IFraudAgent> Agents { get; }
    
    /// <summary>
    /// Initializes the module
    /// </summary>
    Task InitializeAsync(ModuleConfiguration config, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Analyzes context using module-specific logic
    /// </summary>
    Task<ModuleAnalysisResult> AnalyzeAsync(
        AnalysisContext context,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Handles insights from other modules
    /// </summary>
    Task HandleInsightAsync(AgentInsight insight, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alert management service
/// </summary>
public interface IAlertService
{
    Task<FraudAlertDto> CreateAlertAsync(CreateAlertRequest request, CancellationToken cancellationToken = default);
    Task<FraudAlertDto?> GetAlertAsync(Guid tenantId, Guid alertId, CancellationToken cancellationToken = default);
    Task<IEnumerable<FraudAlertDto>> GetAlertsAsync(Guid tenantId, AlertQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<FraudAlertDto> UpdateAlertStatusAsync(Guid tenantId, Guid alertId, UpdateAlertStatusRequest request, CancellationToken cancellationToken = default);
    Task AssignAlertAsync(Guid tenantId, Guid alertId, Guid userId, CancellationToken cancellationToken = default);
    Task EscalateAlertAsync(Guid tenantId, Guid alertId, string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// Tenant management service
/// </summary>
public interface ITenantService
{
    Task<TenantDto> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);
    Task<TenantDto?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantDto> UpdateTenantAsync(Guid tenantId, UpdateTenantRequest request, CancellationToken cancellationToken = default);
    Task<IEnumerable<ModuleType>> GetEnabledModulesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task EnableModuleAsync(Guid tenantId, ModuleType module, CancellationToken cancellationToken = default);
    Task DisableModuleAsync(Guid tenantId, ModuleType module, CancellationToken cancellationToken = default);
    Task<string> RegenerateApiKeyAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
