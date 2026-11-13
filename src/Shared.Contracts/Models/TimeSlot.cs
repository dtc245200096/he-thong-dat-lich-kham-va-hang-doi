namespace Shared.Contracts.Models;

/// <summary>
/// Đại diện cho khung giờ khám của bác sĩ.
/// </summary>
public record TimeSlot
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid DoctorId { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public bool IsBooked { get; init; } = false;
    public int MaxCapacity { get; init; } = 1;
}

