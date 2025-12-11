using Microsoft.Extensions.Logging;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Entities;
using SentinelAI.Core.Enums;
using SentinelAI.Core.Exceptions;
using SentinelAI.Core.Interfaces;

namespace SentinelAI.Application.Services;

/// <summary>
/// Tenant management service implementation
/// </summary>
public class TenantService : ITenantService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEncryptionService _encryptionService;
    private readonly IApiKeyService _apiKeyService;
    private readonly ILogger<TenantService> _logger;
    
    public TenantService(
        IUnitOfWork unitOfWork,
        IEncryptionService encryptionService,
        IApiKeyService apiKeyService,
        ILogger<TenantService> logger)
    {
        _unitOfWork = unitOfWork;
        _encryptionService = encryptionService;
        _apiKeyService = apiKeyService;
        _logger = logger;
    }
    
    public async Task<TenantDto> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        // Check for duplicate code
        var existingTenant = await _unitOfWork.Tenants.FirstOrDefaultAsync(
            t => t.Code == request.Code, cancellationToken);
        
        if (existingTenant != null)
        {
            throw new ConflictException($"Tenant with code '{request.Code}' already exists.");
        }
        
        // Generate API key
        var (apiKey, apiKeyHash) = _apiKeyService.GenerateApiKey();
        
        var tenant = new Tenant
        {
            Name = request.Name,
            Code = request.Code.ToUpperInvariant(),
            Description = request.Description,
            SubscriptionTier = request.SubscriptionTier,
            EnabledModules = request.EnabledModules ?? GetDefaultModules(request.SubscriptionTier),
            WebhookUrl = request.WebhookUrl,
            ApiKeyHash = apiKeyHash,
            ApiKeyExpiresAt = DateTime.UtcNow.AddYears(1),
            EncryptedContactEmail = !string.IsNullOrEmpty(request.ContactEmail) 
                ? _encryptionService.Encrypt(request.ContactEmail) 
                : null,
            EncryptedContactPhone = !string.IsNullOrEmpty(request.ContactPhone) 
                ? _encryptionService.Encrypt(request.ContactPhone) 
                : null
        };
        
        await _unitOfWork.Tenants.AddAsync(tenant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Created tenant {TenantCode} with ID {TenantId}", tenant.Code, tenant.Id);
        
        var dto = MapToDto(tenant);
        // Include the API key only on creation - it won't be retrievable later
        _logger.LogWarning("API Key for tenant {TenantCode}: {ApiKey} - Store this securely, it won't be shown again.", 
            tenant.Code, apiKey);
        
        return dto;
    }
    
    public async Task<TenantDto?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId, cancellationToken);
        return tenant != null ? MapToDto(tenant) : null;
    }
    
    public async Task<TenantDto> UpdateTenantAsync(Guid tenantId, UpdateTenantRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new EntityNotFoundException("Tenant", tenantId);
        
        if (!string.IsNullOrEmpty(request.Name))
            tenant.Name = request.Name;
        
        if (!string.IsNullOrEmpty(request.Description))
            tenant.Description = request.Description;
        
        if (request.SubscriptionTier.HasValue)
            tenant.SubscriptionTier = request.SubscriptionTier.Value;
        
        if (!string.IsNullOrEmpty(request.WebhookUrl))
            tenant.WebhookUrl = request.WebhookUrl;
        
        if (!string.IsNullOrEmpty(request.ContactEmail))
            tenant.EncryptedContactEmail = _encryptionService.Encrypt(request.ContactEmail);
        
        if (!string.IsNullOrEmpty(request.ContactPhone))
            tenant.EncryptedContactPhone = _encryptionService.Encrypt(request.ContactPhone);
        
        if (request.LowRiskThreshold.HasValue)
            tenant.LowRiskThreshold = request.LowRiskThreshold.Value;
        
        if (request.MediumRiskThreshold.HasValue)
            tenant.MediumRiskThreshold = request.MediumRiskThreshold.Value;
        
        if (request.HighRiskThreshold.HasValue)
            tenant.HighRiskThreshold = request.HighRiskThreshold.Value;
        
        if (request.CriticalRiskThreshold.HasValue)
            tenant.CriticalRiskThreshold = request.CriticalRiskThreshold.Value;
        
        tenant.UpdatedAt = DateTime.UtcNow;
        
        await _unitOfWork.Tenants.UpdateAsync(tenant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Updated tenant {TenantId}", tenantId);
        
        return MapToDto(tenant);
    }
    
    public async Task<IEnumerable<ModuleType>> GetEnabledModulesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new EntityNotFoundException("Tenant", tenantId);
        
        return tenant.EnabledModules;
    }
    
    public async Task EnableModuleAsync(Guid tenantId, ModuleType module, CancellationToken cancellationToken = default)
    {
        var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new EntityNotFoundException("Tenant", tenantId);
        
        if (!tenant.EnabledModules.Contains(module))
        {
            tenant.EnabledModules.Add(module);
            tenant.UpdatedAt = DateTime.UtcNow;
            
            await _unitOfWork.Tenants.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Enabled module {Module} for tenant {TenantId}", module, tenantId);
        }
    }
    
    public async Task DisableModuleAsync(Guid tenantId, ModuleType module, CancellationToken cancellationToken = default)
    {
        var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new EntityNotFoundException("Tenant", tenantId);
        
        if (tenant.EnabledModules.Contains(module))
        {
            tenant.EnabledModules.Remove(module);
            tenant.UpdatedAt = DateTime.UtcNow;
            
            await _unitOfWork.Tenants.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Disabled module {Module} for tenant {TenantId}", module, tenantId);
        }
    }
    
    public async Task<string> RegenerateApiKeyAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var apiKey = await _apiKeyService.RotateApiKeyAsync(tenantId, cancellationToken);
        _logger.LogInformation("Regenerated API key for tenant {TenantId}", tenantId);
        return apiKey;
    }
    
    private static List<ModuleType> GetDefaultModules(SubscriptionTier tier) => tier switch
    {
        SubscriptionTier.Starter => new List<ModuleType> { ModuleType.TransactionSentinel },
        SubscriptionTier.Growth => new List<ModuleType> { ModuleType.TransactionSentinel, ModuleType.POSAgentShield },
        SubscriptionTier.Professional => new List<ModuleType> 
        { 
            ModuleType.TransactionSentinel, 
            ModuleType.POSAgentShield, 
            ModuleType.IdentityFortress 
        },
        SubscriptionTier.Enterprise => Enum.GetValues<ModuleType>().ToList(),
        _ => new List<ModuleType> { ModuleType.TransactionSentinel }
    };
    
    private TenantDto MapToDto(Tenant tenant) => new()
    {
        Id = tenant.Id,
        Name = tenant.Name,
        Code = tenant.Code,
        Description = tenant.Description,
        SubscriptionTier = tenant.SubscriptionTier,
        IsActive = tenant.IsActive,
        EnabledModules = tenant.EnabledModules,
        CreatedAt = tenant.CreatedAt,
        WebhookUrl = tenant.WebhookUrl,
        Thresholds = new TenantThresholdsDto
        {
            LowRiskThreshold = tenant.LowRiskThreshold,
            MediumRiskThreshold = tenant.MediumRiskThreshold,
            HighRiskThreshold = tenant.HighRiskThreshold,
            CriticalRiskThreshold = tenant.CriticalRiskThreshold
        }
    };
}
