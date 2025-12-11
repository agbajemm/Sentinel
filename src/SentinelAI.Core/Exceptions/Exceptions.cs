namespace SentinelAI.Core.Exceptions;

/// <summary>
/// Base exception for Sentinel AI
/// </summary>
public abstract class SentinelException : Exception
{
    public string ErrorCode { get; }
    public int StatusCode { get; }
    
    protected SentinelException(string message, string errorCode, int statusCode = 500, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }
}

/// <summary>
/// Thrown when a requested entity is not found
/// </summary>
public class EntityNotFoundException : SentinelException
{
    public string EntityType { get; }
    public object EntityId { get; }
    
    public EntityNotFoundException(string entityType, object entityId)
        : base($"{entityType} with ID '{entityId}' was not found.", "ENTITY_NOT_FOUND", 404)
    {
        EntityType = entityType;
        EntityId = entityId;
    }
}

/// <summary>
/// Thrown when validation fails
/// </summary>
public class ValidationException : SentinelException
{
    public IDictionary<string, string[]> Errors { get; }
    
    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.", "VALIDATION_ERROR", 400)
    {
        Errors = errors;
    }
    
    public ValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { { field, new[] { error } } })
    {
    }
}

/// <summary>
/// Thrown when user is not authorized
/// </summary>
public class UnauthorizedException : SentinelException
{
    public UnauthorizedException(string message = "Authentication required.")
        : base(message, "UNAUTHORIZED", 401)
    {
    }
}

/// <summary>
/// Thrown when user doesn't have permission
/// </summary>
public class ForbiddenException : SentinelException
{
    public ForbiddenException(string message = "You don't have permission to perform this action.")
        : base(message, "FORBIDDEN", 403)
    {
    }
}

/// <summary>
/// Thrown when there's a conflict (e.g., duplicate entry)
/// </summary>
public class ConflictException : SentinelException
{
    public ConflictException(string message)
        : base(message, "CONFLICT", 409)
    {
    }
}

/// <summary>
/// Thrown when rate limit is exceeded
/// </summary>
public class RateLimitExceededException : SentinelException
{
    public int RetryAfterSeconds { get; }
    
    public RateLimitExceededException(int retryAfterSeconds = 60)
        : base("Rate limit exceeded. Please try again later.", "RATE_LIMIT_EXCEEDED", 429)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}

/// <summary>
/// Thrown when an agent fails to process a request
/// </summary>
public class AgentProcessingException : SentinelException
{
    public string AgentId { get; }
    
    public AgentProcessingException(string agentId, string message, Exception? innerException = null)
        : base(message, "AGENT_PROCESSING_ERROR", 500, innerException)
    {
        AgentId = agentId;
    }
}

/// <summary>
/// Thrown when an external service fails
/// </summary>
public class ExternalServiceException : SentinelException
{
    public string ServiceName { get; }
    
    public ExternalServiceException(string serviceName, string message, Exception? innerException = null)
        : base(message, "EXTERNAL_SERVICE_ERROR", 502, innerException)
    {
        ServiceName = serviceName;
    }
}

/// <summary>
/// Thrown when encryption/decryption fails
/// </summary>
public class EncryptionException : SentinelException
{
    public EncryptionException(string message, Exception? innerException = null)
        : base(message, "ENCRYPTION_ERROR", 500, innerException)
    {
    }
}

/// <summary>
/// Thrown when tenant configuration is invalid
/// </summary>
public class TenantConfigurationException : SentinelException
{
    public Guid TenantId { get; }
    
    public TenantConfigurationException(Guid tenantId, string message)
        : base(message, "TENANT_CONFIGURATION_ERROR", 400)
    {
        TenantId = tenantId;
    }
}
