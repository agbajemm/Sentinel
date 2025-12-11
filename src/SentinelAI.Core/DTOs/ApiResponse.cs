namespace SentinelAI.Core.DTOs;

/// <summary>
/// Standard API response wrapper
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
    public string? TraceId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data, string? message = null)
        => new()
        {
            Success = true,
            Data = data,
            Message = message,
            Timestamp = DateTime.UtcNow
        };

    public static ApiResponse<T> Fail(string message, List<string>? errors = null)
        => new()
        {
            Success = false,
            Message = message,
            Errors = errors,
            Timestamp = DateTime.UtcNow
        };
}
