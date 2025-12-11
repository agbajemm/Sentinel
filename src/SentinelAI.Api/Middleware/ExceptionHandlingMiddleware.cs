using System.Net;
using System.Text.Json;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Exceptions;

namespace SentinelAI.Api.Middleware;

/// <summary>
/// Global exception handling middleware
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    
    public ExceptionHandlingMiddleware(
        RequestDelegate next, 
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }
    
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;
        
        var (statusCode, response) = exception switch
        {
            EntityNotFoundException ex => (
                (int)HttpStatusCode.NotFound, 
                CreateErrorResponse(ex.Message, ex.ErrorCode, traceId)),
            
            ValidationException ex => (
                (int)HttpStatusCode.BadRequest,
                CreateValidationErrorResponse(ex.Message, ex.Errors, traceId)),
            
            UnauthorizedException ex => (
                (int)HttpStatusCode.Unauthorized,
                CreateErrorResponse(ex.Message, ex.ErrorCode, traceId)),
            
            ForbiddenException ex => (
                (int)HttpStatusCode.Forbidden,
                CreateErrorResponse(ex.Message, ex.ErrorCode, traceId)),
            
            ConflictException ex => (
                (int)HttpStatusCode.Conflict,
                CreateErrorResponse(ex.Message, ex.ErrorCode, traceId)),
            
            RateLimitExceededException ex => (
                (int)HttpStatusCode.TooManyRequests,
                CreateErrorResponse(ex.Message, ex.ErrorCode, traceId)),
            
            AgentProcessingException ex => (
                (int)HttpStatusCode.InternalServerError,
                CreateErrorResponse("An error occurred during fraud analysis. Please try again.", ex.ErrorCode, traceId)),
            
            ExternalServiceException ex => (
                (int)HttpStatusCode.BadGateway,
                CreateErrorResponse("An external service is currently unavailable.", ex.ErrorCode, traceId)),
            
            EncryptionException ex => (
                (int)HttpStatusCode.InternalServerError,
                CreateErrorResponse("A security error occurred.", ex.ErrorCode, traceId)),
            
            OperationCanceledException => (
                499, // Client Closed Request
                CreateErrorResponse("The request was cancelled.", "REQUEST_CANCELLED", traceId)),
            
            _ => (
                (int)HttpStatusCode.InternalServerError,
                CreateErrorResponse("An unexpected error occurred.", "INTERNAL_ERROR", traceId))
        };
        
        // Log the exception
        if (statusCode >= 500)
        {
            _logger.LogError(exception, 
                "Unhandled exception occurred. TraceId: {TraceId}, Path: {Path}", 
                traceId, context.Request.Path);
        }
        else
        {
            _logger.LogWarning(
                "Request failed with status {StatusCode}. TraceId: {TraceId}, Path: {Path}, Message: {Message}",
                statusCode, traceId, context.Request.Path, exception.Message);
        }
        
        // Add details in development
        if (_environment.IsDevelopment() && statusCode >= 500)
        {
            response.Errors = new List<string> { exception.ToString() };
        }
        
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;
        
        // Add retry-after header for rate limiting
        if (exception is RateLimitExceededException rateLimitEx)
        {
            context.Response.Headers.Append("Retry-After", rateLimitEx.RetryAfterSeconds.ToString());
        }
        
        var jsonOptions = new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        };
        
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
    
    private static ApiResponse<object> CreateErrorResponse(string message, string errorCode, string traceId)
    {
        return new ApiResponse<object>
        {
            Success = false,
            Message = message,
            Errors = new List<string> { errorCode },
            TraceId = traceId,
            Timestamp = DateTime.UtcNow
        };
    }
    
    private static ApiResponse<object> CreateValidationErrorResponse(
        string message, 
        IDictionary<string, string[]> errors, 
        string traceId)
    {
        var errorList = errors.SelectMany(e => e.Value.Select(v => $"{e.Key}: {v}")).ToList();
        
        return new ApiResponse<object>
        {
            Success = false,
            Message = message,
            Errors = errorList,
            TraceId = traceId,
            Timestamp = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Extension method to add the middleware
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
