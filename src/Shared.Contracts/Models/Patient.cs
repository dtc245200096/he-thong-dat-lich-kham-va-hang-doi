namespace Shared.Contracts.Models;

/// <summary>
/// Đại diện cho thông tin Bệnh nhân trong hệ thống.
/// </summary>
public record Patient
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FullName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string? Email { get; init; }
    public DateTime DateOfBirth { get; init; }
    public string? Address { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

