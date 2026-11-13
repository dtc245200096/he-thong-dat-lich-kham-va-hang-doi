using Shared.Contracts.Models;

namespace Shared.Contracts.DTOs;

/// <summary>
/// DTO phản hồi trạng thái hàng đợi hiện tại của một bác sĩ / phòng khám.
/// </summary>
public record QueueStatusResponse
{
    public Guid DoctorId { get; init; }
    public string DoctorName { get; init; } = string.Empty;
    public string RoomNumber { get; init; } = string.Empty;
    public int CurrentServingTicket { get; init; }
    public int TotalWaiting { get; init; }
    public IReadOnlyList<QueueTicket> WaitingTickets { get; init; } = Array.Empty<QueueTicket>();
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
}

