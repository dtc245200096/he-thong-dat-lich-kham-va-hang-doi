using Microsoft.AspNetCore.SignalR;

namespace Notification.Service.Hubs;

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
        _logger.LogInformation("[QueueHub] Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("[QueueHub] Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // =============================================================================
    // TODO: Phase 4 - SignalR Real-time Integration
    // Ở các buổi học tới về SignalR, chúng ta sẽ mở rộng các phương thức:
    // 1. JoinDoctorQueue(doctorId): Client tham gia vào nhóm theo dõi hàng đợi của bác sĩ cụ thể.
    // 2. LeaveDoctorQueue(doctorId): Client rời khỏi nhóm.
    // 3. JoinPatientGroup(patientId): Client bệnh nhân nhận thông báo riêng tư khi đến lượt khám.
    //
    // Hub sẽ được gọi từ RabbitMQ Consumer (hoặc IHubContext<QueueHub>) để broadcast:
    // - "ReceiveQueueUpdate": Gửi cập nhật hàng đợi mới nhất cho toàn bộ client trong phòng chờ.
    // - "ReceiveTicketGenerated": Báo cho bệnh nhân biết số thứ tự của họ đã được cấp.
    // - "CallPatient": Gọi số thứ tự tiếp theo vào phòng khám.
    // =============================================================================

    /// <summary>
    /// Tham gia phòng theo dõi hàng đợi của bác sĩ.
    /// </summary>
    public async Task JoinDoctorQueue(string doctorId)
    {
        _logger.LogInformation("[QueueHub] Client {ConnectionId} joining doctor queue group: {DoctorId}", Context.ConnectionId, doctorId);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"DoctorQueue_{doctorId}");
    }

    /// <summary>
    /// Rời phòng theo dõi hàng đợi của bác sĩ.
    /// </summary>
    public async Task LeaveDoctorQueue(string doctorId)
    {
        _logger.LogInformation("[QueueHub] Client {ConnectionId} leaving doctor queue group: {DoctorId}", Context.ConnectionId, doctorId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"DoctorQueue_{doctorId}");
    }
}

