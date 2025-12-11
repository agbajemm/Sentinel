using System.Text.Json;
using Microsoft.Extensions.Logging;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Entities;
using SentinelAI.Core.Enums;
using SentinelAI.Core.Exceptions;
using SentinelAI.Core.Interfaces;

namespace SentinelAI.Application.Services;

/// <summary>
/// Alert management service implementation
/// </summary>
public class AlertService : IAlertService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AlertService> _logger;
    
    public AlertService(IUnitOfWork unitOfWork, ILogger<AlertService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }
    
    public async Task<FraudAlertDto> CreateAlertAsync(CreateAlertRequest request, CancellationToken cancellationToken = default)
    {
        var alertReference = GenerateAlertReference();
        
        var alert = new FraudAlert
        {
            TenantId = request.TenantId,
            TransactionAnalysisId = request.TransactionAnalysisId,
            AlertReference = alertReference,
            Status = AlertStatus.New,
            RiskLevel = request.RiskLevel,
            RiskScore = request.RiskScore,
            Title = request.Title,
            Description = request.Description,
            SourceModule = request.SourceModule,
            SourceAgentId = request.SourceAgentId,
            RiskFactors = request.RiskFactors != null ? JsonSerializer.Serialize(request.RiskFactors) : null,
            RecommendedActions = request.RecommendedActions != null ? JsonSerializer.Serialize(request.RecommendedActions) : null
        };
        
        await _unitOfWork.FraudAlerts.AddAsync(alert, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Created fraud alert {AlertReference} for tenant {TenantId}", alertReference, request.TenantId);
        
        return MapToDto(alert);
    }
    
    public async Task<FraudAlertDto?> GetAlertAsync(Guid tenantId, Guid alertId, CancellationToken cancellationToken = default)
    {
        var alert = await _unitOfWork.FraudAlerts.FirstOrDefaultAsync(
            a => a.Id == alertId && a.TenantId == tenantId,
            cancellationToken);
        
        return alert != null ? MapToDto(alert) : null;
    }
    
    public async Task<IEnumerable<FraudAlertDto>> GetAlertsAsync(
        Guid tenantId, 
        AlertQueryParameters parameters, 
        CancellationToken cancellationToken = default)
    {
        var alerts = await _unitOfWork.FraudAlerts.FindAsync(
            a => a.TenantId == tenantId,
            cancellationToken);
        
        var query = alerts.AsQueryable();
        
        // Apply filters
        if (parameters.Status.HasValue)
            query = query.Where(a => a.Status == parameters.Status.Value);
        
        if (parameters.RiskLevel.HasValue)
            query = query.Where(a => a.RiskLevel == parameters.RiskLevel.Value);
        
        if (parameters.Module.HasValue)
            query = query.Where(a => a.SourceModule == parameters.Module.Value);
        
        if (parameters.FromDate.HasValue)
            query = query.Where(a => a.CreatedAt >= parameters.FromDate.Value);
        
        if (parameters.ToDate.HasValue)
            query = query.Where(a => a.CreatedAt <= parameters.ToDate.Value);
        
        if (parameters.AssignedToUserId.HasValue)
            query = query.Where(a => a.AssignedToUserId == parameters.AssignedToUserId.Value);
        
        if (parameters.IsEscalated.HasValue)
            query = query.Where(a => a.IsEscalated == parameters.IsEscalated.Value);
        
        // Apply sorting
        query = parameters.SortBy?.ToLower() switch
        {
            "riskscore" => parameters.SortDescending 
                ? query.OrderByDescending(a => a.RiskScore) 
                : query.OrderBy(a => a.RiskScore),
            "status" => parameters.SortDescending 
                ? query.OrderByDescending(a => a.Status) 
                : query.OrderBy(a => a.Status),
            "risklevel" => parameters.SortDescending 
                ? query.OrderByDescending(a => a.RiskLevel) 
                : query.OrderBy(a => a.RiskLevel),
            _ => parameters.SortDescending 
                ? query.OrderByDescending(a => a.CreatedAt) 
                : query.OrderBy(a => a.CreatedAt)
        };
        
        // Apply pagination
        var skip = (parameters.Page - 1) * parameters.PageSize;
        query = query.Skip(skip).Take(parameters.PageSize);
        
        return query.Select(MapToDto);
    }
    
    public async Task<FraudAlertDto> UpdateAlertStatusAsync(
        Guid tenantId, 
        Guid alertId, 
        UpdateAlertStatusRequest request, 
        CancellationToken cancellationToken = default)
    {
        var alert = await _unitOfWork.FraudAlerts.FirstOrDefaultAsync(
            a => a.Id == alertId && a.TenantId == tenantId,
            cancellationToken) ?? throw new EntityNotFoundException("Alert", alertId);
        
        var oldStatus = alert.Status;
        alert.Status = request.Status;
        alert.UpdatedAt = DateTime.UtcNow;
        
        if (request.Status == AlertStatus.Resolved || request.Status == AlertStatus.FalsePositive || request.Status == AlertStatus.Confirmed)
        {
            alert.ResolvedAt = DateTime.UtcNow;
            alert.ResolutionNotes = request.Notes;
        }
        
        await _unitOfWork.FraudAlerts.UpdateAsync(alert, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "Updated alert {AlertId} status from {OldStatus} to {NewStatus}",
            alertId, oldStatus, request.Status);
        
        return MapToDto(alert);
    }
    
    public async Task AssignAlertAsync(Guid tenantId, Guid alertId, Guid userId, CancellationToken cancellationToken = default)
    {
        var alert = await _unitOfWork.FraudAlerts.FirstOrDefaultAsync(
            a => a.Id == alertId && a.TenantId == tenantId,
            cancellationToken) ?? throw new EntityNotFoundException("Alert", alertId);
        
        alert.AssignedToUserId = userId;
        alert.AssignedAt = DateTime.UtcNow;
        alert.Status = AlertStatus.Investigating;
        alert.UpdatedAt = DateTime.UtcNow;
        
        await _unitOfWork.FraudAlerts.UpdateAsync(alert, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Assigned alert {AlertId} to user {UserId}", alertId, userId);
    }
    
    public async Task EscalateAlertAsync(Guid tenantId, Guid alertId, string reason, CancellationToken cancellationToken = default)
    {
        var alert = await _unitOfWork.FraudAlerts.FirstOrDefaultAsync(
            a => a.Id == alertId && a.TenantId == tenantId,
            cancellationToken) ?? throw new EntityNotFoundException("Alert", alertId);
        
        alert.IsEscalated = true;
        alert.EscalatedAt = DateTime.UtcNow;
        alert.EscalationReason = reason;
        alert.Status = AlertStatus.Escalated;
        alert.UpdatedAt = DateTime.UtcNow;
        
        await _unitOfWork.FraudAlerts.UpdateAsync(alert, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        _logger.LogWarning("Alert {AlertId} escalated. Reason: {Reason}", alertId, reason);
    }
    
    private static string GenerateAlertReference()
    {
        return $"ALT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }
    
    private static FraudAlertDto MapToDto(FraudAlert alert) => new()
    {
        Id = alert.Id,
        AlertReference = alert.AlertReference,
        Status = alert.Status,
        RiskLevel = alert.RiskLevel,
        RiskScore = alert.RiskScore,
        Title = alert.Title,
        Description = alert.Description,
        RiskFactors = !string.IsNullOrEmpty(alert.RiskFactors) 
            ? JsonSerializer.Deserialize<List<string>>(alert.RiskFactors) ?? new() 
            : new(),
        RecommendedActions = !string.IsNullOrEmpty(alert.RecommendedActions) 
            ? JsonSerializer.Deserialize<List<string>>(alert.RecommendedActions) ?? new() 
            : new(),
        SourceModule = alert.SourceModule,
        AssignedToUserId = alert.AssignedToUserId,
        CreatedAt = alert.CreatedAt,
        ResolvedAt = alert.ResolvedAt,
        IsEscalated = alert.IsEscalated
    };
}
