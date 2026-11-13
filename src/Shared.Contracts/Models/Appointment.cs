using Shared.Contracts.Enums;

namespace Shared.Contracts.Models;

/// <summary>
/// Đại diện cho một lịch hẹn khám bệnh.
/// </summary>
public record Appointment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PatientId { get; init; }
    public Guid DoctorId { get; init; }
    public Guid TimeSlotId { get; init; }
    public DateTime AppointmentDate { get; init; }
    public AppointmentStatus Status { get; init; } = AppointmentStatus.Pending;
    public string? Reason { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; init; }
}

