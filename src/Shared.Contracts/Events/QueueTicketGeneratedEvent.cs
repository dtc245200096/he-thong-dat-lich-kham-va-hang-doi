namespace Shared.Contracts.Events;

/// <summary>
/// Sự kiện phát sinh khi Queue.Worker đã sinh số thứ tự thành công cho lịch hẹn,
/// gửi sang RabbitMQ để Notification.Service tiếp nhận và bắn SignalR về Client.
/// </summary>
public record QueueTicketGeneratedEvent
{
    public Guid TicketId { get; init; }
    public Guid AppointmentId { get; init; }
    public Guid PatientId { get; init; }
    public Guid DoctorId { get; init; }
    public int TicketNumber { get; init; }
    public DateTime EstimatedTime { get; init; }
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}

