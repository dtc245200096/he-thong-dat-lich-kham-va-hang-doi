namespace Shared.Contracts.Events;

/// <summary>
/// Sự kiện thông báo thay đổi trạng thái hàng đợi (số đang khám, số tiếp theo, hoàn tất),
/// dùng để cập nhật bảng điện tử và màn hình theo dõi của bệnh nhân qua SignalR.
/// </summary>
public record QueueUpdatedEvent
{
    public Guid DoctorId { get; init; }
    public int CurrentTicketNumber { get; init; }
    public int NextTicketNumber { get; init; }
    public int RemainingWaitingCount { get; init; }
    public string Status { get; init; } = "Serving";
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

