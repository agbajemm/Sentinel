using FluentAssertions;
using FluentValidation.TestHelper;
using SentinelAI.Application.Validators;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Enums;
using Xunit;

namespace SentinelAI.Tests.Unit;

public class TransactionAnalysisRequestValidatorTests
{
    private readonly TransactionAnalysisRequestValidator _validator;
    
    public TransactionAnalysisRequestValidatorTests()
    {
        _validator = new TransactionAnalysisRequestValidator();
    }
    
    [Fact]
    public void Validate_ValidRequest_ShouldPass()
    {
        // Arrange
        var request = TestDataGenerators.GenerateTransactionRequest();
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
    
    [Fact]
    public void Validate_EmptyTransactionId_ShouldFail()
    {
        // Arrange
        var request = TestDataGenerators.GenerateTransactionRequest();
        request.TransactionId = string.Empty;
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldHaveValidationErrorFor(r => r.TransactionId);
    }
    
    [Fact]
    public void Validate_TransactionIdTooLong_ShouldFail()
    {
        // Arrange
        var request = TestDataGenerators.GenerateTransactionRequest();
        request.TransactionId = new string('X', 101);
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldHaveValidationErrorFor(r => r.TransactionId)
            .WithErrorMessage("Transaction ID cannot exceed 100 characters");
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Validate_InvalidAmount_ShouldFail(decimal amount)
    {
        // Arrange
        var request = TestDataGenerators.GenerateTransactionRequest(amount: amount);
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Amount);
    }
    
    [Fact]
    public void Validate_AmountExceedsMaximum_ShouldFail()
    {
        // Arrange
        var request = TestDataGenerators.GenerateTransactionRequest(amount: 2_000_000_000);
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Amount)
            .WithErrorMessage("Amount exceeds maximum allowed value");
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("NGNA")]
    [InlineData("ngn")]
    public void Validate_InvalidCurrency_ShouldFail(string currency)
    {
        // Arrange
        var request = TestDataGenerators.GenerateTransactionRequest();
        request.Currency = currency;
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Currency);
    }
    
    [Theory]
    [InlineData("NGN")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void Validate_ValidCurrency_ShouldPass(string currency)
    {
        // Arrange
        var request = TestDataGenerators.GenerateTransactionRequest();
        request.Currency = currency;
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.Currency);
    }
    
    [Fact]
    public void Validate_FutureTransactionTime_ShouldFail()
    {
        // Arrange
        var request = TestDataGenerators.GenerateTransactionRequest();
        request.TransactionTime = DateTime.UtcNow.AddHours(1);
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldHaveValidationErrorFor(r => r.TransactionTime)
            .WithErrorMessage("Transaction time cannot be in the future");
    }
    
    [Theory]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.1")]
    [InlineData("255.255.255.255")]
    public void Validate_ValidIpAddress_ShouldPass(string ipAddress)
    {
        // Arrange
        var request = TestDataGenerators.GenerateTransactionRequest();
        request.IpAddress = ipAddress;
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.IpAddress);
    }
    
    [Theory]
    [InlineData("invalid")]
    [InlineData("192.168.1")]
    [InlineData("300.300.300.300")]
    public void Validate_InvalidIpAddress_ShouldFail(string ipAddress)
    {
        // Arrange
        var request = TestDataGenerators.GenerateTransactionRequest();
        request.IpAddress = ipAddress;
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldHaveValidationErrorFor(r => r.IpAddress);
    }
    
    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    public void Validate_InvalidLatitude_ShouldFail(double latitude)
    {
        // Arrange
        var request = TestDataGenerators.GenerateTransactionRequest();
        request.Latitude = latitude;
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Latitude);
    }
    
    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    public void Validate_InvalidLongitude_ShouldFail(double longitude)
    {
        // Arrange
        var request = TestDataGenerators.GenerateTransactionRequest();
        request.Longitude = longitude;
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Longitude);
    }
    
    [Theory]
    [InlineData(500)]
    [InlineData(70000)]
    public void Validate_InvalidTimeout_ShouldFail(int timeoutMs)
    {
        // Arrange
        var request = TestDataGenerators.GenerateTransactionRequest();
        request.TimeoutMs = timeoutMs;
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldHaveValidationErrorFor(r => r.TimeoutMs);
    }
}

public class CreateTenantRequestValidatorTests
{
    private readonly CreateTenantRequestValidator _validator;
    
    public CreateTenantRequestValidatorTests()
    {
        _validator = new CreateTenantRequestValidator();
    }
    
    [Fact]
    public void Validate_ValidRequest_ShouldPass()
    {
        // Arrange
        var request = TestDataGenerators.GenerateCreateTenantRequest();
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
    
    [Fact]
    public void Validate_EmptyName_ShouldFail()
    {
        // Arrange
        var request = TestDataGenerators.GenerateCreateTenantRequest();
        request.Name = string.Empty;
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Name);
    }
    
    [Fact]
    public void Validate_NameTooLong_ShouldFail()
    {
        // Arrange
        var request = TestDataGenerators.GenerateCreateTenantRequest();
        request.Name = new string('X', 201);
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Name)
            .WithErrorMessage("Tenant name cannot exceed 200 characters");
    }
    
    [Theory]
    [InlineData("Invalid Code!")]
    [InlineData("Code With Spaces")]
    [InlineData("Code@Special")]
    public void Validate_InvalidCode_ShouldFail(string code)
    {
        // Arrange
        var request = TestDataGenerators.GenerateCreateTenantRequest();
        request.Code = code;
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Code);
    }
    
    [Theory]
    [InlineData("VALID_CODE")]
    [InlineData("Valid-Code-123")]
    [InlineData("Code123")]
    public void Validate_ValidCode_ShouldPass(string code)
    {
        // Arrange
        var request = TestDataGenerators.GenerateCreateTenantRequest();
        request.Code = code;
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.Code);
    }
    
    [Fact]
    public void Validate_HttpWebhookUrl_ShouldFail()
    {
        // Arrange
        var request = TestDataGenerators.GenerateCreateTenantRequest();
        request.WebhookUrl = "http://example.com/webhook";
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldHaveValidationErrorFor(r => r.WebhookUrl)
            .WithErrorMessage("Webhook URL must be a valid HTTPS URL");
    }
    
    [Fact]
    public void Validate_InvalidEmail_ShouldFail()
    {
        // Arrange
        var request = TestDataGenerators.GenerateCreateTenantRequest();
        request.ContactEmail = "invalid-email";
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldHaveValidationErrorFor(r => r.ContactEmail);
    }
    
    [Theory]
    [InlineData("123")]
    [InlineData("invalid-phone")]
    [InlineData("++234123456789")]
    public void Validate_InvalidPhone_ShouldFail(string phone)
    {
        // Arrange
        var request = TestDataGenerators.GenerateCreateTenantRequest();
        request.ContactPhone = phone;
        
        // Act
        var result = _validator.TestValidate(request);
        
        // Assert
        result.ShouldHaveValidationErrorFor(r => r.ContactPhone);
    }
}

public class AlertQueryParametersValidatorTests
{
    private readonly AlertQueryParametersValidator _validator;
    
    public AlertQueryParametersValidatorTests()
    {
        _validator = new AlertQueryParametersValidator();
    }
    
    [Fact]
    public void Validate_DefaultParameters_ShouldPass()
    {
        // Arrange
        var parameters = new AlertQueryParameters();
        
        // Act
        var result = _validator.TestValidate(parameters);
        
        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidPage_ShouldFail(int page)
    {
        // Arrange
        var parameters = new AlertQueryParameters { Page = page };
        
        // Act
        var result = _validator.TestValidate(parameters);
        
        // Assert
        result.ShouldHaveValidationErrorFor(p => p.Page);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_InvalidPageSize_ShouldFail(int pageSize)
    {
        // Arrange
        var parameters = new AlertQueryParameters { PageSize = pageSize };
        
        // Act
        var result = _validator.TestValidate(parameters);
        
        // Assert
        result.ShouldHaveValidationErrorFor(p => p.PageSize);
    }
    
    [Fact]
    public void Validate_FromDateAfterToDate_ShouldFail()
    {
        // Arrange
        var parameters = new AlertQueryParameters
        {
            FromDate = DateTime.UtcNow,
            ToDate = DateTime.UtcNow.AddDays(-1)
        };
        
        // Act
        var result = _validator.TestValidate(parameters);
        
        // Assert
        result.ShouldHaveValidationErrorFor(p => p.FromDate);
    }
}
