namespace Shared.Contracts.DTOs;

/// <summary>
/// DTO phản hồi lỗi chuẩn (Problem/Error Details) từ các dịch vụ trong hệ thống.
/// </summary>
public record ApiErrorResponse
{
    public int StatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Details { get; init; }
    public IDictionary<string, string[]>? ValidationErrors { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

