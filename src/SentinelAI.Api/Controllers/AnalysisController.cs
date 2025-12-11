using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Interfaces;

namespace SentinelAI.Api.Controllers;

/// <summary>
/// API controller for fraud analysis operations
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[Produces("application/json")]
public class AnalysisController : ControllerBase
{
    private readonly IFraudDetectionService _fraudDetectionService;
    private readonly ILogger<AnalysisController> _logger;
    
    public AnalysisController(
        IFraudDetectionService fraudDetectionService,
        ILogger<AnalysisController> logger)
    {
        _fraudDetectionService = fraudDetectionService;
        _logger = logger;
    }
    
    /// <summary>
    /// Analyze a single transaction for fraud
    /// </summary>
    /// <param name="request">Transaction analysis request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Fraud analysis result</returns>
    [HttpPost("transaction")]
    [ProducesResponseType(typeof(ApiResponse<FraudAnalysisResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<FraudAnalysisResponse>>> AnalyzeTransaction(
        [FromBody] TransactionAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantIdFromClaims();
        if (tenantId == Guid.Empty)
        {
            return Unauthorized(ApiResponse<object>.Fail("Invalid or missing tenant"));
        }
        
        _logger.LogInformation("Received analysis request for transaction {TransactionId}", request.TransactionId);
        
        var result = await _fraudDetectionService.AnalyzeTransactionAsync(request, tenantId, cancellationToken);
        
        return Ok(ApiResponse<FraudAnalysisResponse>.Ok(result, "Analysis completed successfully"));
    }
    
    /// <summary>
    /// Analyze multiple transactions in batch
    /// </summary>
    /// <param name="requests">List of transaction analysis requests</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of fraud analysis results</returns>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<FraudAnalysisResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<IEnumerable<FraudAnalysisResponse>>>> AnalyzeBatch(
        [FromBody] List<TransactionAnalysisRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count > 100)
        {
            return BadRequest(ApiResponse<object>.Fail("Batch size cannot exceed 100 transactions"));
        }
        
        var tenantId = GetTenantIdFromClaims();
        if (tenantId == Guid.Empty)
        {
            return Unauthorized(ApiResponse<object>.Fail("Invalid or missing tenant"));
        }
        
        _logger.LogInformation("Received batch analysis request for {Count} transactions", requests.Count);
        
        var results = await _fraudDetectionService.AnalyzeBatchAsync(requests, tenantId, cancellationToken);
        
        return Ok(ApiResponse<IEnumerable<FraudAnalysisResponse>>.Ok(results, $"Analyzed {requests.Count} transactions"));
    }
    
    /// <summary>
    /// Get analysis history for a customer
    /// </summary>
    /// <param name="customerIdHash">Hashed customer ID</param>
    /// <param name="limit">Maximum number of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of historical analysis results</returns>
    [HttpGet("history/{customerIdHash}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<FraudAnalysisResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<FraudAnalysisResponse>>>> GetCustomerHistory(
        string customerIdHash,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdFromClaims();
        
        var results = await _fraudDetectionService.GetCustomerHistoryAsync(
            tenantId, customerIdHash, limit, cancellationToken);
        
        return Ok(ApiResponse<IEnumerable<FraudAnalysisResponse>>.Ok(results));
    }
    
    private Guid GetTenantIdFromClaims()
    {
        var tenantIdClaim = User.FindFirst(Core.Constants.ClaimTypes.TenantId)?.Value;
        return Guid.TryParse(tenantIdClaim, out var tenantId) ? tenantId : Guid.Empty;
    }
}
