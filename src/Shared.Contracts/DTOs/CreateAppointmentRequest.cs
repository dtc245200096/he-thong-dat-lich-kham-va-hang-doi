namespace Shared.Contracts.DTOs;

/// <summary>
/// DTO tiếp nhận yêu cầu đặt lịch khám mới từ Client.
/// </summary>
public record CreateAppointmentRequest
{
    public Guid PatientId { get; init; }
    public Guid DoctorId { get; init; }
    public Guid TimeSlotId { get; init; }
    public DateTime AppointmentDate { get; init; }
    public string? Reason { get; init; }
}

