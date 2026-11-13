namespace Shared.Contracts.Models;

/// <summary>
/// Đại diện cho thông tin Bác sĩ khám bệnh.
/// </summary>
public record Doctor
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FullName { get; init; } = string.Empty;
    public string Specialty { get; init; } = string.Empty;
    public string RoomNumber { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
}

