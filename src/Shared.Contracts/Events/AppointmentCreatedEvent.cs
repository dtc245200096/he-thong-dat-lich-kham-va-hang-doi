namespace Shared.Contracts.Events;

/// <summary>
/// Sự kiện phát sinh khi một lịch hẹn khám được tạo thành công trên Appointment.Api,
/// gửi sang RabbitMQ để Queue.Worker tiếp nhận và xử lý cấp số thứ tự.
/// </summary>
public record AppointmentCreatedEvent
{
    public Guid AppointmentId { get; init; }
    public Guid PatientId { get; init; }
    public Guid DoctorId { get; init; }
    public Guid TimeSlotId { get; init; }
    public DateTime AppointmentDate { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

