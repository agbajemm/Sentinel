using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Interfaces;

namespace SentinelAI.Api.Controllers;

/// <summary>
/// API controller for fraud alert management
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[Produces("application/json")]
public class AlertsController : ControllerBase
{
    private readonly IAlertService _alertService;
    private readonly ILogger<AlertsController> _logger;
    
    public AlertsController(IAlertService alertService, ILogger<AlertsController> logger)
    {
        _alertService = alertService;
        _logger = logger;
    }
    
    /// <summary>
    /// Get alerts with filtering and pagination
    /// </summary>
    /// <param name="parameters">Query parameters for filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of alerts</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<FraudAlertDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<FraudAlertDto>>>> GetAlerts(
        [FromQuery] AlertQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantIdFromClaims();
        var alerts = await _alertService.GetAlertsAsync(tenantId, parameters, cancellationToken);
        
        return Ok(ApiResponse<IEnumerable<FraudAlertDto>>.Ok(alerts));
    }
    
    /// <summary>
    /// Get a specific alert by ID
    /// </summary>
    /// <param name="id">Alert ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Alert details</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<FraudAlertDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<FraudAlertDto>>> GetAlert(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantIdFromClaims();
        var alert = await _alertService.GetAlertAsync(tenantId, id, cancellationToken);
        
        if (alert == null)
        {
            return NotFound(ApiResponse<object>.Fail($"Alert with ID {id} not found"));
        }
        
        return Ok(ApiResponse<FraudAlertDto>.Ok(alert));
    }
    
    /// <summary>
    /// Update alert status
    /// </summary>
    /// <param name="id">Alert ID</param>
    /// <param name="request">Status update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated alert</returns>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<FraudAlertDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<FraudAlertDto>>> UpdateAlertStatus(
        Guid id,
        [FromBody] UpdateAlertStatusRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantIdFromClaims();
        
        _logger.LogInformation("Updating alert {AlertId} status to {Status}", id, request.Status);
        
        var alert = await _alertService.UpdateAlertStatusAsync(tenantId, id, request, cancellationToken);
        
        return Ok(ApiResponse<FraudAlertDto>.Ok(alert, "Alert status updated successfully"));
    }
    
    /// <summary>
    /// Assign alert to a user
    /// </summary>
    /// <param name="id">Alert ID</param>
    /// <param name="userId">User ID to assign</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpPost("{id:guid}/assign/{userId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> AssignAlert(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantIdFromClaims();
        
        _logger.LogInformation("Assigning alert {AlertId} to user {UserId}", id, userId);
        
        await _alertService.AssignAlertAsync(tenantId, id, userId, cancellationToken);
        
        return Ok(ApiResponse<object>.Ok(null!, "Alert assigned successfully"));
    }
    
    /// <summary>
    /// Escalate an alert
    /// </summary>
    /// <param name="id">Alert ID</param>
    /// <param name="request">Escalation request with reason</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpPost("{id:guid}/escalate")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> EscalateAlert(
        Guid id,
        [FromBody] EscalateAlertRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantIdFromClaims();
        
        _logger.LogWarning("Escalating alert {AlertId}. Reason: {Reason}", id, request.Reason);
        
        await _alertService.EscalateAlertAsync(tenantId, id, request.Reason, cancellationToken);
        
        return Ok(ApiResponse<object>.Ok(null!, "Alert escalated successfully"));
    }
    
    private Guid GetTenantIdFromClaims()
    {
        var tenantIdClaim = User.FindFirst(Core.Constants.ClaimTypes.TenantId)?.Value;
        return Guid.TryParse(tenantIdClaim, out var tenantId) ? tenantId : Guid.Empty;
    }
}

/// <summary>
/// Request to escalate an alert
/// </summary>
public class EscalateAlertRequest
{
    public required string Reason { get; set; }
}
