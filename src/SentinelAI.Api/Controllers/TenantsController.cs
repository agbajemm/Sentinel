using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SentinelAI.Core.Constants;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Enums;
using SentinelAI.Core.Interfaces;

namespace SentinelAI.Api.Controllers;

/// <summary>
/// API controller for tenant management
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ILogger<TenantsController> _logger;
    
    public TenantsController(ITenantService tenantService, ILogger<TenantsController> logger)
    {
        _tenantService = tenantService;
        _logger = logger;
    }
    
    /// <summary>
    /// Create a new tenant
    /// </summary>
    /// <param name="request">Tenant creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created tenant with API key</returns>
    [HttpPost]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponse<TenantDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<TenantDto>>> CreateTenant(
        [FromBody] CreateTenantRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating new tenant: {TenantCode}", request.Code);
        
        var tenant = await _tenantService.CreateTenantAsync(request, cancellationToken);
        
        return CreatedAtAction(
            nameof(GetTenant), 
            new { id = tenant.Id }, 
            ApiResponse<TenantDto>.Ok(tenant, "Tenant created successfully. API key has been logged securely."));
    }
    
    /// <summary>
    /// Get tenant by ID
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant details</returns>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<TenantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TenantDto>>> GetTenant(
        Guid id,
        CancellationToken cancellationToken)
    {
        // Verify user has access to this tenant
        var userTenantId = GetTenantIdFromClaims();
        if (userTenantId != id && !User.IsInRole(Roles.SuperAdmin))
        {
            return Forbid();
        }
        
        var tenant = await _tenantService.GetTenantAsync(id, cancellationToken);
        
        if (tenant == null)
        {
            return NotFound(ApiResponse<object>.Fail($"Tenant with ID {id} not found"));
        }
        
        return Ok(ApiResponse<TenantDto>.Ok(tenant));
    }
    
    /// <summary>
    /// Update tenant settings
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <param name="request">Update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated tenant</returns>
    [HttpPatch("{id:guid}")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.TenantAdmin}")]
    [ProducesResponseType(typeof(ApiResponse<TenantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TenantDto>>> UpdateTenant(
        Guid id,
        [FromBody] UpdateTenantRequest request,
        CancellationToken cancellationToken)
    {
        // Verify user has access to this tenant
        var userTenantId = GetTenantIdFromClaims();
        if (userTenantId != id && !User.IsInRole(Roles.SuperAdmin))
        {
            return Forbid();
        }
        
        _logger.LogInformation("Updating tenant {TenantId}", id);
        
        var tenant = await _tenantService.UpdateTenantAsync(id, request, cancellationToken);
        
        return Ok(ApiResponse<TenantDto>.Ok(tenant, "Tenant updated successfully"));
    }
    
    /// <summary>
    /// Get enabled modules for tenant
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of enabled modules</returns>
    [HttpGet("{id:guid}/modules")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ModuleType>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<ModuleType>>>> GetEnabledModules(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userTenantId = GetTenantIdFromClaims();
        if (userTenantId != id && !User.IsInRole(Roles.SuperAdmin))
        {
            return Forbid();
        }
        
        var modules = await _tenantService.GetEnabledModulesAsync(id, cancellationToken);
        
        return Ok(ApiResponse<IEnumerable<ModuleType>>.Ok(modules));
    }
    
    /// <summary>
    /// Enable a module for tenant
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <param name="module">Module to enable</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpPost("{id:guid}/modules/{module}")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> EnableModule(
        Guid id,
        ModuleType module,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Enabling module {Module} for tenant {TenantId}", module, id);
        
        await _tenantService.EnableModuleAsync(id, module, cancellationToken);
        
        return Ok(ApiResponse<object>.Ok(null!, $"Module {module} enabled successfully"));
    }
    
    /// <summary>
    /// Disable a module for tenant
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <param name="module">Module to disable</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpDelete("{id:guid}/modules/{module}")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> DisableModule(
        Guid id,
        ModuleType module,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Disabling module {Module} for tenant {TenantId}", module, id);
        
        await _tenantService.DisableModuleAsync(id, module, cancellationToken);
        
        return Ok(ApiResponse<object>.Ok(null!, $"Module {module} disabled successfully"));
    }
    
    /// <summary>
    /// Regenerate API key for tenant
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New API key (shown only once)</returns>
    [HttpPost("{id:guid}/api-key/regenerate")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.TenantAdmin}")]
    [ProducesResponseType(typeof(ApiResponse<ApiKeyResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ApiKeyResponse>>> RegenerateApiKey(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userTenantId = GetTenantIdFromClaims();
        if (userTenantId != id && !User.IsInRole(Roles.SuperAdmin))
        {
            return Forbid();
        }
        
        _logger.LogWarning("Regenerating API key for tenant {TenantId}", id);
        
        var apiKey = await _tenantService.RegenerateApiKeyAsync(id, cancellationToken);
        
        return Ok(ApiResponse<ApiKeyResponse>.Ok(
            new ApiKeyResponse { ApiKey = apiKey },
            "API key regenerated. Store this securely - it won't be shown again."));
    }
    
    private Guid GetTenantIdFromClaims()
    {
        var tenantIdClaim = User.FindFirst(Core.Constants.ClaimTypes.TenantId)?.Value;
        return Guid.TryParse(tenantIdClaim, out var tenantId) ? tenantId : Guid.Empty;
    }
}

/// <summary>
/// Response containing the API key
/// </summary>
public class ApiKeyResponse
{
    public required string ApiKey { get; set; }
}
