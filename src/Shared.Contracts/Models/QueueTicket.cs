namespace Shared.Contracts.Models;

/// <summary>
/// Đại diện cho số thứ tự (Ticket) trong hàng đợi khám của phòng khám.
/// </summary>
public record QueueTicket
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AppointmentId { get; init; }
    public Guid DoctorId { get; init; }
    public int TicketNumber { get; init; }
    public DateTime EstimatedTime { get; init; }
    public string Status { get; init; } = "Waiting"; // Waiting, Serving, Completed, Skipped
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

