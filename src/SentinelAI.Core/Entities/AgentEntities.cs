using SentinelAI.Core.Enums;

namespace SentinelAI.Core.Entities;

/// <summary>
/// Configuration for an AI agent
/// </summary>
public class AgentConfiguration : AuditableEntity
{
    public required string AgentId { get; set; }  // Azure AI Foundry agent ID
    public required string Name { get; set; }
    public string? Description { get; set; }
    
    public ModuleType Module { get; set; }
    public AgentLevel Level { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Azure AI Foundry configuration
    public string? AzureAgentId { get; set; }
    public string? ModelDeploymentName { get; set; }
    public double Temperature { get; set; } = 0.1;
    public int MaxTokens { get; set; } = 4000;
    
    // Agent-specific settings (JSON)
    public string? Settings { get; set; }
    
    // Tools available to this agent (JSON array of tool names)
    public string? AvailableTools { get; set; }
    
    // Performance metrics
    public double AverageResponseTimeMs { get; set; }
    public int TotalInvocations { get; set; }
    public double SuccessRate { get; set; }
}

/// <summary>
/// Log of agent invocations for debugging and analytics
/// </summary>
public class AgentInvocationLog : BaseEntity
{
    public Guid TenantId { get; set; }
    public required string AgentId { get; set; }
    public Guid? TransactionAnalysisId { get; set; }
    
    // Request/Response
    public string? RequestContext { get; set; }  // JSON (sanitized, no PII)
    public string? ResponseSummary { get; set; }
    public decimal? ConfidenceScore { get; set; }
    
    // Performance
    public int ResponseTimeMs { get; set; }
    public int TokensUsed { get; set; }
    
    // Status
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    
    // Tools used during invocation
    public string? ToolsInvoked { get; set; }  // JSON array
}

/// <summary>
/// Inter-agent message for tracking agent communication
/// </summary>
public class AgentMessage : BaseEntity
{
    public required string MessageId { get; set; }
    public required string SourceAgentId { get; set; }
    public string? TargetAgentId { get; set; }  // Null for broadcasts
    
    public AgentMessageIntent Intent { get; set; }
    public string? Context { get; set; }  // JSON
    public string? Payload { get; set; }  // JSON
    
    public decimal Confidence { get; set; }
    public string? ReasoningChain { get; set; }  // JSON array of reasoning steps
    
    // Correlation
    public string? CorrelationId { get; set; }
    public string? ParentMessageId { get; set; }
}
