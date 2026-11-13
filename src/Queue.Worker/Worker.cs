using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Events;

namespace Queue.Worker;

/// <summary>
/// BackgroundService chịu trách nhiệm xử lý hàng đợi:
/// 1. Lắng nghe sự kiện AppointmentCreatedEvent từ RabbitMQ.
/// 2. Cấp số thứ tự (Ticket Number) tuần tự cho từng bác sĩ/phòng khám.
/// 3. Lưu vé khám (QueueTicket) và cập nhật trạng thái lịch hẹn vào Database.
/// 4. Phát tán sự kiện QueueTicketGeneratedEvent & QueueUpdatedEvent sang RabbitMQ.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rabbitHost = _configuration["RabbitMQ:HostName"] ?? "localhost";
        var appointmentQueue = _configuration["RabbitMQ:AppointmentCreatedQueue"] ?? "clinic.appointment.created.queue";

        _logger.LogInformation("==========================================================");
        _logger.LogInformation(">>> [Queue.Worker] Started listening for RabbitMQ events.");
        _logger.LogInformation(">>> RabbitMQ Target: {Host} | Queue: {Queue}", rabbitHost, appointmentQueue);
        _logger.LogInformation("==========================================================");

        // =============================================================================
        // TODO: Phase 3 - RabbitMQ Consumer Integration
        // Ở buổi học RabbitMQ, chúng ta sẽ thực hiện:
        // 1. Tạo kết nối ConnectionFactory tới RabbitMQ broker.
        // 2. Khai báo Exchange và Queue (channel.QueueDeclare, channel.QueueBind).
        // 3. Khởi tạo EventingBasicConsumer để tiêu thụ message bất đồng bộ.
        // 4. Nhận body JSON -> Deserialize thành AppointmentCreatedEvent.
        // 5. Kết nối Database:
        //    - Tra cứu số thứ tự lớn nhất hiện tại của bác sĩ trong ngày.
        //    - Cấp TicketNumber mới = maxTicket + 1.
        //    - Lưu QueueTicket vào bảng QueueTickets.
        //    - Cập nhật trạng thái Appointment -> Confirmed.
        // 6. RabbitMQ Producer:
        //    - Gửi QueueTicketGeneratedEvent sang Notification.Service.
        //    - Gửi QueueUpdatedEvent để cập nhật bảng điện tử.
        // 7. Gửi channel.BasicAck để xác nhận message đã xử lý xong.
        // =============================================================================

        while (!stoppingToken.IsCancellationRequested)
        {
            // Heartbeat log mô phỏng worker đang sẵn sàng
            _logger.LogInformation("[Queue.Worker] Standing by for new appointment events at: {Time}", DateTimeOffset.UtcNow);

            // Mô phỏng chu kỳ kiểm tra/heartbeat (thay thế bằng event-driven consumer ở Phase 3)
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation(">>> [Queue.Worker] Service is stopping gracefully.");
    }
}
