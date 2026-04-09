namespace Shared.Contracts.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public T? Data { get; set; }
    public string TraceId { get; set; } = string.Empty;
}
