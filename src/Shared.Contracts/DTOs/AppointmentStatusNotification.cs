using Shared.Contracts.Enums;

namespace Shared.Contracts.DTOs;

/// <summary>
/// DTO thông báo trạng thái hàng đợi và tiến trình lịch hẹn gửi qua SignalR.
/// </summary>
public record AppointmentStatusNotification
{
    public Guid AppointmentId { get; init; }
    public Guid PatientId { get; init; }
    public Guid DoctorId { get; init; }
    public AppointmentStatus Status { get; init; }
    public int? TicketNumber { get; init; }
    public string? Message { get; init; }
    public DateTime? EstimatedTime { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
