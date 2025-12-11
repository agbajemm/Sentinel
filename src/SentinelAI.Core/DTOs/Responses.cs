using SentinelAI.Core.Enums;

namespace SentinelAI.Core.DTOs;

/// <summary>
/// Response from fraud analysis
/// </summary>
public class FraudAnalysisResponse
{
    public required string TransactionId { get; set; }
    public required string AnalysisId { get; set; }
    
    // Risk assessment
    public decimal RiskScore { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public RecommendedAction RecommendedAction { get; set; }
    
    // Details
    public List<RiskFactor> RiskFactors { get; set; } = new();
    public string? Explanation { get; set; }
    
    // Module results
    public List<ModuleResult> ModuleResults { get; set; } = new();
    
    // Metadata
    public int ProcessingTimeMs { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    public List<string> ModulesInvoked { get; set; } = new();
    public List<string> AgentsInvoked { get; set; } = new();
    
    // Alert info (if generated)
    public Guid? AlertId { get; set; }
    public string? AlertReference { get; set; }
}

/// <summary>
/// Individual risk factor identified
/// </summary>
public class RiskFactor
{
    public required string Code { get; set; }
    public required string Description { get; set; }
    public decimal Weight { get; set; }
    public decimal Contribution { get; set; }
    public string? Source { get; set; }
    public Dictionary<string, object>? Details { get; set; }
}

/// <summary>
/// Result from a specific module
/// </summary>
public class ModuleResult
{
    public ModuleType Module { get; set; }
    public decimal RiskScore { get; set; }
    public decimal Confidence { get; set; }
    public List<RiskFactor> RiskFactors { get; set; } = new();
    public int ProcessingTimeMs { get; set; }
    public string? Explanation { get; set; }
}

/// <summary>
/// Response from agent
/// </summary>
public class AgentResponse
{
    public required string AgentId { get; set; }
    public bool IsSuccess { get; set; }
    public decimal RiskScore { get; set; }
    public decimal Confidence { get; set; }
    public List<RiskFactor> RiskFactors { get; set; } = new();
    public string? Explanation { get; set; }
    public string? ReasoningChain { get; set; }
    public int ProcessingTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Response from orchestrator
/// </summary>
public class OrchestratorResponse
{
    public required string CorrelationId { get; set; }
    public bool IsSuccess { get; set; }
    public decimal AggregatedRiskScore { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public RecommendedAction RecommendedAction { get; set; }
    public List<AgentResponse> AgentResponses { get; set; } = new();
    public List<RiskFactor> AggregatedRiskFactors { get; set; } = new();
    public string? Explanation { get; set; }
    public int TotalProcessingTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Analysis result from a module
/// </summary>
public class ModuleAnalysisResult
{
    public ModuleType Module { get; set; }
    public bool IsSuccess { get; set; }
    public decimal RiskScore { get; set; }
    public decimal Confidence { get; set; }
    public List<RiskFactor> RiskFactors { get; set; } = new();
    public string? Explanation { get; set; }
    public Dictionary<string, object>? Insights { get; set; }
    public int ProcessingTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Tenant DTO for API responses
/// </summary>
public class TenantDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Code { get; set; }
    public string? Description { get; set; }
    public SubscriptionTier SubscriptionTier { get; set; }
    public bool IsActive { get; set; }
    public List<ModuleType> EnabledModules { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public string? WebhookUrl { get; set; }
    public TenantThresholdsDto? Thresholds { get; set; }
}

/// <summary>
/// Tenant risk thresholds
/// </summary>
public class TenantThresholdsDto
{
    public decimal LowRiskThreshold { get; set; }
    public decimal MediumRiskThreshold { get; set; }
    public decimal HighRiskThreshold { get; set; }
    public decimal CriticalRiskThreshold { get; set; }
}

/// <summary>
/// Fraud alert DTO for API responses
/// </summary>
public class FraudAlertDto
{
    public Guid Id { get; set; }
    public required string AlertReference { get; set; }
    public AlertStatus Status { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public decimal RiskScore { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public List<string> RiskFactors { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
    public ModuleType SourceModule { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool IsEscalated { get; set; }
}

/// <summary>
/// Paginated response wrapper
/// </summary>
public class PaginatedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
