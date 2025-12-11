using SentinelAI.Core.Enums;

namespace SentinelAI.Core.DTOs;

/// <summary>
/// Request to analyze a transaction for fraud
/// </summary>
public class TransactionAnalysisRequest
{
    public required string TransactionId { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public TransactionChannel Channel { get; set; }
    public DateTime TransactionTime { get; set; } = DateTime.UtcNow;
    
    // Parties
    public required string SourceAccount { get; set; }
    public required string DestinationAccount { get; set; }
    public string? CustomerId { get; set; }
    public string? BeneficiaryName { get; set; }
    
    // Device/Session context
    public string? DeviceFingerprint { get; set; }
    public string? IpAddress { get; set; }
    public string? SessionId { get; set; }
    public string? UserAgent { get; set; }
    
    // Location
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    
    // Additional context
    public string? TransactionType { get; set; }
    public string? Narration { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    
    // Options
    public bool IncludeExplanation { get; set; } = true;
    public int? TimeoutMs { get; set; }
    public List<ModuleType>? ModulesToInvoke { get; set; }
}

/// <summary>
/// Request to create a new tenant
/// </summary>
public class CreateTenantRequest
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public string? Description { get; set; }
    public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Starter;
    public List<ModuleType>? EnabledModules { get; set; }
    public string? WebhookUrl { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
}

/// <summary>
/// Request to update a tenant
/// </summary>
public class UpdateTenantRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public SubscriptionTier? SubscriptionTier { get; set; }
    public string? WebhookUrl { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public decimal? LowRiskThreshold { get; set; }
    public decimal? MediumRiskThreshold { get; set; }
    public decimal? HighRiskThreshold { get; set; }
    public decimal? CriticalRiskThreshold { get; set; }
}

/// <summary>
/// Request to create a fraud alert
/// </summary>
public class CreateAlertRequest
{
    public Guid TenantId { get; set; }
    public Guid? TransactionAnalysisId { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public decimal RiskScore { get; set; }
    public ModuleType SourceModule { get; set; }
    public string? SourceAgentId { get; set; }
    public List<string>? RiskFactors { get; set; }
    public List<string>? RecommendedActions { get; set; }
}

/// <summary>
/// Request to update alert status
/// </summary>
public class UpdateAlertStatusRequest
{
    public AlertStatus Status { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Query parameters for listing alerts
/// </summary>
public class AlertQueryParameters
{
    public AlertStatus? Status { get; set; }
    public RiskLevel? RiskLevel { get; set; }
    public ModuleType? Module { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public bool? IsEscalated { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// Request for agent orchestration
/// </summary>
public class OrchestratorRequest
{
    public Guid TenantId { get; set; }
    public required TransactionAnalysisRequest Transaction { get; set; }
    public List<ModuleType>? RequiredModules { get; set; }
    public Dictionary<string, object>? Context { get; set; }
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Request sent to an individual agent
/// </summary>
public class AgentRequest
{
    public required string RequestId { get; set; }
    public Guid TenantId { get; set; }
    public required TransactionAnalysisRequest Transaction { get; set; }
    public Dictionary<string, object>? Context { get; set; }
    public string? ParentAgentId { get; set; }
    public string? CorrelationId { get; set; }
}

#region Authentication DTOs

// Remove the duplicated authentication DTOs here; they now live in AuthDtos.cs

#endregion
