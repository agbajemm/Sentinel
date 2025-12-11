using SentinelAI.Core.Enums;

namespace SentinelAI.Core.DTOs;

/// <summary>
/// Insight shared between agents
/// </summary>
public class AgentInsight
{
    public required string InsightId { get; set; }
    public required string SourceAgentId { get; set; }
    public ModuleType SourceModule { get; set; }
    public required string InsightType { get; set; }
    public decimal Confidence { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Tool available to an agent
/// </summary>
public class AgentTool
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public Dictionary<string, ToolParameter> Parameters { get; set; } = new();
}

/// <summary>
/// Parameter for an agent tool
/// </summary>
public class ToolParameter
{
    public required string Type { get; set; }
    public required string Description { get; set; }
    public bool Required { get; set; }
    public object? DefaultValue { get; set; }
}

/// <summary>
/// Module configuration
/// </summary>
public class ModuleConfiguration
{
    public ModuleType ModuleType { get; set; }
    public bool IsEnabled { get; set; }
    public Dictionary<string, object> Settings { get; set; } = new();
    public List<AgentConfiguration> AgentConfigs { get; set; } = new();
}

/// <summary>
/// Agent configuration
/// </summary>
public class AgentConfiguration
{
    public required string AgentId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public AgentLevel Level { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? AzureAgentId { get; set; }
    public string? ModelDeploymentName { get; set; }
    public double Temperature { get; set; } = 0.1;
    public int MaxTokens { get; set; } = 4000;
    public Dictionary<string, object> Settings { get; set; } = new();
    public List<string> AvailableTools { get; set; } = new();
}

/// <summary>
/// Context passed during analysis
/// </summary>
public class AnalysisContext
{
    public required string CorrelationId { get; set; }
    public Guid TenantId { get; set; }
    public required TransactionAnalysisRequest Transaction { get; set; }
    public Dictionary<string, object> SharedContext { get; set; } = new();
    public List<AgentInsight> Insights { get; set; } = new();
    public List<RiskFactor> AccumulatedRiskFactors { get; set; } = new();
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Health check result for agents
/// </summary>
public class AgentHealthStatus
{
    public required string AgentId { get; set; }
    public required string AgentName { get; set; }
    public ModuleType Module { get; set; }
    public bool IsHealthy { get; set; }
    public int ResponseTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// System health overview
/// </summary>
public class SystemHealthResponse
{
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = "Unknown";
    public List<AgentHealthStatus> AgentStatuses { get; set; } = new();
    public Dictionary<string, bool> DependencyStatuses { get; set; } = new();
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}
