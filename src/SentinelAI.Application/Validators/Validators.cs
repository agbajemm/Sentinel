using FluentValidation;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Enums;

namespace SentinelAI.Application.Validators;

/// <summary>
/// Validator for transaction analysis requests
/// </summary>
public class TransactionAnalysisRequestValidator : AbstractValidator<TransactionAnalysisRequest>
{
    public TransactionAnalysisRequestValidator()
    {
        RuleFor(x => x.TransactionId)
            .NotEmpty().WithMessage("Transaction ID is required")
            .MaximumLength(100).WithMessage("Transaction ID cannot exceed 100 characters");
        
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero")
            .LessThanOrEqualTo(1_000_000_000).WithMessage("Amount exceeds maximum allowed value");
        
        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required")
            .Length(3).WithMessage("Currency must be a 3-letter ISO code")
            .Matches("^[A-Z]{3}$").WithMessage("Currency must be uppercase letters");
        
        RuleFor(x => x.Channel)
            .IsInEnum().WithMessage("Invalid transaction channel");
        
        RuleFor(x => x.SourceAccount)
            .NotEmpty().WithMessage("Source account is required")
            .MaximumLength(50).WithMessage("Source account cannot exceed 50 characters");
        
        RuleFor(x => x.DestinationAccount)
            .NotEmpty().WithMessage("Destination account is required")
            .MaximumLength(50).WithMessage("Destination account cannot exceed 50 characters");
        
        RuleFor(x => x.TransactionTime)
            .NotEmpty().WithMessage("Transaction time is required")
            .LessThanOrEqualTo(DateTime.UtcNow.AddMinutes(5)).WithMessage("Transaction time cannot be in the future");
        
        RuleFor(x => x.IpAddress)
            .Matches(@"^(?:[0-9]{1,3}\.){3}[0-9]{1,3}$|^([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$")
            .When(x => !string.IsNullOrEmpty(x.IpAddress))
            .WithMessage("Invalid IP address format");
        
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .When(x => x.Latitude.HasValue)
            .WithMessage("Latitude must be between -90 and 90");
        
        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .When(x => x.Longitude.HasValue)
            .WithMessage("Longitude must be between -180 and 180");
        
        RuleFor(x => x.TimeoutMs)
            .InclusiveBetween(1000, 60000)
            .When(x => x.TimeoutMs.HasValue)
            .WithMessage("Timeout must be between 1000ms and 60000ms");
    }
}

/// <summary>
/// Validator for tenant creation requests
/// </summary>
public class CreateTenantRequestValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tenant name is required")
            .MinimumLength(2).WithMessage("Tenant name must be at least 2 characters")
            .MaximumLength(200).WithMessage("Tenant name cannot exceed 200 characters");
        
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Tenant code is required")
            .MinimumLength(2).WithMessage("Tenant code must be at least 2 characters")
            .MaximumLength(50).WithMessage("Tenant code cannot exceed 50 characters")
            .Matches("^[A-Za-z0-9_-]+$").WithMessage("Tenant code can only contain letters, numbers, hyphens, and underscores");
        
        RuleFor(x => x.SubscriptionTier)
            .IsInEnum().WithMessage("Invalid subscription tier");
        
        RuleFor(x => x.WebhookUrl)
            .Must(BeAValidUrl)
            .When(x => !string.IsNullOrEmpty(x.WebhookUrl))
            .WithMessage("Webhook URL must be a valid HTTPS URL");
        
        RuleFor(x => x.ContactEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrEmpty(x.ContactEmail))
            .WithMessage("Contact email must be a valid email address");
        
        RuleFor(x => x.ContactPhone)
            .Matches(@"^\+?[0-9]{10,15}$")
            .When(x => !string.IsNullOrEmpty(x.ContactPhone))
            .WithMessage("Contact phone must be a valid phone number");
    }
    
    private static bool BeAValidUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return true;
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult) 
               && uriResult.Scheme == Uri.UriSchemeHttps;
    }
}

/// <summary>
/// Validator for alert query parameters
/// </summary>
public class AlertQueryParametersValidator : AbstractValidator<AlertQueryParameters>
{
    public AlertQueryParametersValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1");
        
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");
        
        RuleFor(x => x.FromDate)
            .LessThanOrEqualTo(x => x.ToDate)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("From date must be before or equal to To date");
        
        RuleFor(x => x.Status)
            .IsInEnum()
            .When(x => x.Status.HasValue)
            .WithMessage("Invalid alert status");
        
        RuleFor(x => x.RiskLevel)
            .IsInEnum()
            .When(x => x.RiskLevel.HasValue)
            .WithMessage("Invalid risk level");
        
        RuleFor(x => x.Module)
            .IsInEnum()
            .When(x => x.Module.HasValue)
            .WithMessage("Invalid module type");
    }
}

/// <summary>
/// Validator for alert status update requests
/// </summary>
public class UpdateAlertStatusRequestValidator : AbstractValidator<UpdateAlertStatusRequest>
{
    public UpdateAlertStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid alert status");
        
        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrEmpty(x.Notes))
            .WithMessage("Notes cannot exceed 2000 characters");
    }
}
