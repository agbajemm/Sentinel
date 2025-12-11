namespace SentinelAI.Core.Constants;

/// <summary>
/// Risk factor codes used throughout the system
/// </summary>
public static class RiskFactorCodes
{
    // Transaction-related
    public const string HighAmountForCustomer = "RF001";
    public const string UnusualTransactionTime = "RF002";
    public const string VelocityThresholdExceeded = "RF003";
    public const string FirstTimeRecipient = "RF004";
    public const string HighRiskCountry = "RF005";
    public const string GeographicAnomaly = "RF006";
    public const string DeviceChanged = "RF007";
    public const string SessionAnomaly = "RF008";
    
    // POS/Agent-related
    public const string AgentHighRiskScore = "RF100";
    public const string TerminalLocationMismatch = "RF101";
    public const string SuspiciousCashOutPattern = "RF102";
    public const string HighBinDiversityTerminal = "RF103";
    public const string NewAgentHighVolume = "RF104";
    public const string MuleNetworkIndicator = "RF105";
    
    // Identity-related
    public const string SyntheticIdentityIndicator = "RF200";
    public const string BvnMismatch = "RF201";
    public const string RecentSimSwap = "RF202";
    public const string DeviceLinkedToMultipleAccounts = "RF203";
    public const string ThinFileHighRisk = "RF204";
    
    // Insider threat-related
    public const string UnusualAccessPattern = "RF300";
    public const string OffHoursActivity = "RF301";
    public const string MakerCheckerBypass = "RF302";
    public const string DormantAccountReactivation = "RF303";
    public const string HighVolumeDataAccess = "RF304";
    
    // Social engineering-related
    public const string ScamIndicatorDetected = "RF400";
    public const string CoercedTransactionPattern = "RF401";
    public const string SuspiciousNarration = "RF402";
}

/// <summary>
/// Agent identifiers
/// </summary>
public static class AgentIds
{
    // Orchestration
    public const string MasterOrchestrator = "AGT-ORCH-001";
    
    // Transaction Sentinel (Module 1)
    public const string TransactionAnalyzer = "AGT-TXN-001";
    public const string BehaviorProfiler = "AGT-TXN-002";
    public const string RiskScorer = "AGT-TXN-003";
    
    // POS/Agent Shield (Module 2)
    public const string TerminalMonitor = "AGT-POS-001";
    public const string AgentProfiler = "AGT-POS-002";
    public const string MuleHunter = "AGT-POS-003";
    public const string GeoValidator = "AGT-POS-004";
    
    // Identity Fortress (Module 3)
    public const string IdentityVerifier = "AGT-IDN-001";
    public const string SyntheticDetector = "AGT-IDN-002";
    public const string GraphAnalyzer = "AGT-IDN-003";
    
    // Insider Threat (Module 4)
    public const string EmployeeBehaviorAnalyzer = "AGT-INS-001";
    public const string ControlBypassDetector = "AGT-INS-002";
    
    // Network Intelligence (Module 5)
    public const string GraphBuilder = "AGT-NET-001";
    public const string NetworkAnalyzer = "AGT-NET-002";
    
    // Investigation Assistant (Module 6)
    public const string CaseBuilder = "AGT-INV-001";
    public const string ReportGenerator = "AGT-INV-002";
}

/// <summary>
/// Cache keys
/// </summary>
public static class CacheKeys
{
    public const string TenantPrefix = "tenant:";
    public const string CustomerProfilePrefix = "customer:profile:";
    public const string AgentConfigPrefix = "agent:config:";
    public const string RateLimitPrefix = "ratelimit:";
    public const string SessionPrefix = "session:";
    
    public static string GetTenantKey(Guid tenantId) => $"{TenantPrefix}{tenantId}";
    public static string GetCustomerProfileKey(Guid tenantId, string customerHash) => $"{CustomerProfilePrefix}{tenantId}:{customerHash}";
    public static string GetAgentConfigKey(string agentId) => $"{AgentConfigPrefix}{agentId}";
    public static string GetRateLimitKey(Guid tenantId) => $"{RateLimitPrefix}{tenantId}";
}

/// <summary>
/// Claim types for JWT
/// </summary>
public static class ClaimTypes
{
    public const string TenantId = "tenant_id";
    public const string UserId = "user_id";
    public const string Role = "role";
    public const string Permissions = "permissions";
}

/// <summary>
/// User roles
/// </summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string TenantAdmin = "TenantAdmin";
    public const string FraudAnalyst = "FraudAnalyst";
    public const string Investigator = "Investigator";
    public const string Viewer = "Viewer";
}

/// <summary>
/// Permissions
/// </summary>
public static class Permissions
{
    public const string ViewTransactions = "transactions:view";
    public const string AnalyzeTransactions = "transactions:analyze";
    public const string ViewAlerts = "alerts:view";
    public const string ManageAlerts = "alerts:manage";
    public const string ViewReports = "reports:view";
    public const string GenerateReports = "reports:generate";
    public const string ManageTenant = "tenant:manage";
    public const string ManageUsers = "users:manage";
    public const string ManageModules = "modules:manage";

    // Additional permissions used by AuthenticationService
    public const string ManageTenants = "tenants:manage";
    public const string ManageSettings = "settings:manage";
    public const string ViewAuditLogs = "auditlogs:view";
}
