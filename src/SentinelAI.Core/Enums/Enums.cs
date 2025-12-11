namespace SentinelAI.Core.Enums;

/// <summary>
/// Available fraud detection modules
/// </summary>
public enum ModuleType
{
    TransactionSentinel = 1,
    POSAgentShield = 2,
    IdentityFortress = 3,
    InsiderThreat = 4,
    NetworkIntelligence = 5,
    InvestigationAssistant = 6
}

/// <summary>
/// Agent hierarchy levels
/// </summary>
public enum AgentLevel
{
    Orchestration = 1,  // L1 - Master orchestrator
    Domain = 2,         // L2 - Module coordinators
    Specialist = 3,     // L3 - Task-specific agents
    Utility = 4         // L4 - Support agents
}

/// <summary>
/// Fraud risk levels
/// </summary>
public enum RiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Recommended actions for fraud detection
/// </summary>
public enum RecommendedAction
{
    Approve = 1,
    Challenge = 2,      // Additional authentication required
    Review = 3,         // Manual review needed
    Block = 4,          // Block transaction
    Alert = 5           // Allow but alert
}

/// <summary>
/// Transaction channels
/// </summary>
public enum TransactionChannel
{
    NIP = 1,
    USSD = 2,
    Mobile = 3,
    Web = 4,
    POS = 5,
    ATM = 6,
    Branch = 7,
    Agent = 8
}

/// <summary>
/// Agent message intents for inter-agent communication
/// </summary>
public enum AgentMessageIntent
{
    Request = 1,
    Response = 2,
    Alert = 3,
    Insight = 4,
    Broadcast = 5
}

/// <summary>
/// Alert status for fraud alerts
/// </summary>
public enum AlertStatus
{
    New = 1,
    Investigating = 2,
    Escalated = 3,
    Resolved = 4,
    FalsePositive = 5,
    Confirmed = 6
}

/// <summary>
/// Tenant subscription tiers
/// </summary>
public enum SubscriptionTier
{
    Starter = 1,
    Growth = 2,
    Professional = 3,
    Enterprise = 4
}
