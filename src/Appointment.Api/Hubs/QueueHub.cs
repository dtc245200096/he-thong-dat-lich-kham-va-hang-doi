using Microsoft.AspNetCore.SignalR;

namespace Appointment.Api.Hubs;

/// <summary>
/// SignalR Hub quản lý kết nối realtime giữa server và các client (bệnh nhân, bác sĩ, màn hình phòng chờ).
/// </summary>
public class QueueHub : Hub
{
    private readonly ILogger<QueueHub> _logger;

    public QueueHub(ILogger<QueueHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("[QueueHub] Client kết nối thành công: ConnectionId = {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("[QueueHub] Client ngắt kết nối: ConnectionId = {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Tham gia phòng theo dõi hàng đợi của bác sĩ.
    /// </summary>
    public async Task JoinDoctorQueue(string doctorId)
    {
        var groupName = $"DoctorQueue_{doctorId}";
        _logger.LogInformation("[QueueHub] Client {ConnectionId} tham gia nhóm bác sĩ: {GroupName}", Context.ConnectionId, groupName);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Rời phòng theo dõi hàng đợi của bác sĩ.
    /// </summary>
    public async Task LeaveDoctorQueue(string doctorId)
    {
        var groupName = $"DoctorQueue_{doctorId}";
        _logger.LogInformation("[QueueHub] Client {ConnectionId} rời nhóm bác sĩ: {GroupName}", Context.ConnectionId, groupName);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Tham gia nhóm theo dõi riêng cho một lịch hẹn (Định tuyến & Cách ly - Yêu cầu 3).
    /// </summary>
    public async Task JoinAppointmentGroup(string appointmentId)
    {
        var groupName = $"Appointment_{appointmentId}";
        _logger.LogInformation("[QueueHub] Client {ConnectionId} tham gia nhóm lịch hẹn: {GroupName}", Context.ConnectionId, groupName);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Rời nhóm theo dõi riêng của lịch hẹn.
    /// </summary>
    public async Task LeaveAppointmentGroup(string appointmentId)
    {
        var groupName = $"Appointment_{appointmentId}";
        _logger.LogInformation("[QueueHub] Client {ConnectionId} rời nhóm lịch hẹn: {GroupName}", Context.ConnectionId, groupName);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}
