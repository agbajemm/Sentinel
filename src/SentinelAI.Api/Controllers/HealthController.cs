using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Interfaces;

namespace SentinelAI.Api.Controllers;

/// <summary>
/// API controller for health checks
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly IAgentOrchestrator _orchestrator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<HealthController> _logger;
    
    public HealthController(
        IAgentOrchestrator orchestrator,
        IUnitOfWork unitOfWork,
        ILogger<HealthController> logger)
    {
        _orchestrator = orchestrator;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }
    
    /// <summary>
    /// Simple liveness check
    /// </summary>
    /// <returns>OK if service is running</returns>
    [HttpGet("live")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult LivenessCheck()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
    
    /// <summary>
    /// Detailed readiness check including dependencies
    /// </summary>
    /// <returns>Health status of all components</returns>
    [HttpGet("ready")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SystemHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SystemHealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SystemHealthResponse>> ReadinessCheck(CancellationToken cancellationToken)
    {
        var response = new SystemHealthResponse
        {
            CheckedAt = DateTime.UtcNow
        };
        
        // Check database connectivity
        try
        {
            await _unitOfWork.Tenants.CountAsync(cancellationToken: cancellationToken);
            response.DependencyStatuses["database"] = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            response.DependencyStatuses["database"] = false;
        }
        
        // Set overall status
        response.IsHealthy = response.DependencyStatuses.All(d => d.Value);
        response.Status = response.IsHealthy ? "healthy" : "degraded";
        
        return response.IsHealthy ? Ok(response) : StatusCode(503, response);
    }
    
    /// <summary>
    /// Check health of all AI agents
    /// </summary>
    /// <returns>Health status of each agent</returns>
    [HttpGet("agents")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<AgentHealthStatus>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<AgentHealthStatus>>>> CheckAgentHealth(
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantIdFromClaims();
        var agents = await _orchestrator.GetEnabledAgentsAsync(tenantId, cancellationToken);
        
        var healthStatuses = new List<AgentHealthStatus>();
        
        foreach (var agent in agents)
        {
            var startTime = DateTime.UtcNow;
            var isHealthy = await agent.IsHealthyAsync(cancellationToken);
            var responseTime = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            
            healthStatuses.Add(new AgentHealthStatus
            {
                AgentId = agent.AgentId,
                AgentName = agent.Name,
                Module = agent.Module,
                IsHealthy = isHealthy,
                ResponseTimeMs = responseTime,
                CheckedAt = DateTime.UtcNow
            });
        }
        
        return Ok(ApiResponse<IEnumerable<AgentHealthStatus>>.Ok(healthStatuses));
    }
    
    private Guid GetTenantIdFromClaims()
    {
        var tenantIdClaim = User.FindFirst(Core.Constants.ClaimTypes.TenantId)?.Value;
        return Guid.TryParse(tenantIdClaim, out var tenantId) ? tenantId : Guid.Empty;
    }
}
