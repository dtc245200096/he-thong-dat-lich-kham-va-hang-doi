using Shared.Contracts.DTOs;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;

var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// CẤU HÌNH HỆ THỐNG & ĐỌC BIẾN MÔI TRƯỜNG / APPSETTINGS
// Tuyệt đối không hardcode chuỗi kết nối hay địa chỉ server
// =============================================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var rabbitMqHost = builder.Configuration["RabbitMQ:HostName"] ?? "localhost";
var rabbitMqPort = builder.Configuration.GetValue<int>("RabbitMQ:Port", 5672);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

var logger = app.Logger;
logger.LogInformation(">>> [Appointment.Api] Starting service...");
logger.LogInformation(">>> Database Connection: {ConnectionString}", 
    string.IsNullOrEmpty(connectionString) ? "Not Configured" : "Configured from Environment/Config");
logger.LogInformation(">>> RabbitMQ Broker: {Host}:{Port}", rabbitMqHost, rabbitMqPort);

// =============================================================================
// ROOT & HEALTH CHECK
// =============================================================================
app.MapGet("/", () => Results.Ok(new
{
    Service = "Appointment.Api",
    Status = "Healthy",
    Version = "1.0.0",
    Timestamp = DateTime.UtcNow
}));

// =============================================================================
// 6 ENDPOINTS KHUNG CHUẨN CỦA ĐỀ TÀI 09
// Các endpoint hiện trả về dữ liệu mẫu (Mock) và được chú thích rõ vị trí
// sẽ tích hợp Database (EF Core/Dapper) & RabbitMQ Producer ở các buổi học sau.
// =============================================================================

// 1. GET /doctors - Lấy danh sách bác sĩ đang hoạt động
app.MapGet("/doctors", (ILogger<Program> log) =>
{
    log.LogInformation("[GET /doctors] Fetching active doctors list");

    // TODO: Phase 2 - Query danh sách bác sĩ từ Database (PostgreSQL/SQL Server)
    var mockDoctors = new List<Doctor>
    {
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), FullName = "BS. Nguyễn Văn A", Specialty = "Nội tổng quát", RoomNumber = "Phòng 101", IsActive = true },
        new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), FullName = "BS. Trần Thị B", Specialty = "Tim mạch", RoomNumber = "Phòng 102", IsActive = true },
        new() { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), FullName = "BS. Lê Văn C", Specialty = "Nhi khoa", RoomNumber = "Phòng 103", IsActive = true }
    };

    return Results.Ok(mockDoctors);
});

// 2. GET /doctors/{id}/slots - Lấy danh sách khung giờ trống của bác sĩ
app.MapGet("/doctors/{id:guid}/slots", (Guid id, ILogger<Program> log) =>
{
    log.LogInformation("[GET /doctors/{DoctorId}/slots] Fetching available slots", id);

    // TODO: Phase 2 - Lấy danh sách khung giờ trống từ Database theo DoctorId và ngày hiện tại
    var now = DateTime.Today.AddHours(8);
    var mockSlots = new List<TimeSlot>
    {
        new() { Id = Guid.NewGuid(), DoctorId = id, StartTime = now, EndTime = now.AddMinutes(30), IsBooked = false },
        new() { Id = Guid.NewGuid(), DoctorId = id, StartTime = now.AddMinutes(30), EndTime = now.AddMinutes(60), IsBooked = false },
        new() { Id = Guid.NewGuid(), DoctorId = id, StartTime = now.AddMinutes(60), EndTime = now.AddMinutes(90), IsBooked = true }
    };

    return Results.Ok(mockSlots);
});

// 3. POST /appointments - Tiếp nhận yêu cầu đặt lịch khám từ Client
app.MapPost("/appointments", (CreateAppointmentRequest request, ILogger<Program> log) =>
{
    log.LogInformation("[POST /appointments] Receiving appointment request for Patient {PatientId} with Doctor {DoctorId}",
        request.PatientId, request.DoctorId);

    // TODO: Phase 2 - Lưu thông tin lịch hẹn vào Database với trạng thái Pending
    var appointment = new Appointment
    {
        Id = Guid.NewGuid(),
        PatientId = request.PatientId,
        DoctorId = request.DoctorId,
        TimeSlotId = request.TimeSlotId,
        AppointmentDate = request.AppointmentDate == default ? DateTime.UtcNow : request.AppointmentDate,
        Status = AppointmentStatus.Pending,
        Reason = request.Reason,
        CreatedAt = DateTime.UtcNow
    };

    // TODO: Phase 3 - Bắn sự kiện AppointmentCreatedEvent lên RabbitMQ (clinic.appointment.exchange)
    // để Queue.Worker tiêu thụ và sinh số thứ tự tự động.
    log.LogInformation("[RabbitMQ Placeholder] Will publish AppointmentCreatedEvent for AppointmentId {AppointmentId}", appointment.Id);

    return Results.Created($"/appointments/{appointment.Id}", new
    {
        Message = "Yêu cầu đặt lịch khám đã được tiếp nhận thành công và đang chờ xếp hàng đợi.",
        Appointment = appointment
    });
});

// 4. DELETE /appointments/{id} - Hủy lịch hẹn khám
app.MapDelete("/appointments/{id:guid}", (Guid id, ILogger<Program> log) =>
{
    log.LogInformation("[DELETE /appointments/{Id}] Request to cancel appointment", id);

    // TODO: Phase 2 - Cập nhật trạng thái Appointment thành Cancelled trong Database
    // TODO: Phase 3 - Bắn sự kiện AppointmentCancelledEvent lên RabbitMQ để cập nhật lại hàng đợi
    return Results.Ok(new
    {
        Message = $"Lịch hẹn {id} đã được hủy thành công (Mock).",
        AppointmentId = id,
        Status = AppointmentStatus.Cancelled
    });
});

// 5. GET /appointments/{id} - Tra cứu chi tiết một lịch hẹn
app.MapGet("/appointments/{id:guid}", (Guid id, ILogger<Program> log) =>
{
    log.LogInformation("[GET /appointments/{Id}] Looking up appointment details", id);

    // TODO: Phase 2 - Tìm kiếm Appointment trong Database theo Id
    var mockAppointment = new Appointment
    {
        Id = id,
        PatientId = Guid.NewGuid(),
        DoctorId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        TimeSlotId = Guid.NewGuid(),
        AppointmentDate = DateTime.UtcNow.Date.AddHours(9),
        Status = AppointmentStatus.Confirmed,
        Reason = "Khám sức khỏe tổng quát",
        CreatedAt = DateTime.UtcNow.AddMinutes(-15)
    };

    return Results.Ok(mockAppointment);
});

// 6. GET /queues/{doctorId} - Lấy trạng thái hàng đợi hiện tại của phòng khám/bác sĩ
app.MapGet("/queues/{doctorId:guid}", (Guid doctorId, ILogger<Program> log) =>
{
    log.LogInformation("[GET /queues/{DoctorId}] Getting queue status", doctorId);

    // TODO: Phase 2 & 3 - Lấy trạng thái hàng đợi từ Database / Redis cache đồng bộ từ Queue.Worker
    var mockQueueStatus = new QueueStatusResponse
    {
        DoctorId = doctorId,
        DoctorName = "BS. Nguyễn Văn A",
        RoomNumber = "Phòng 101",
        CurrentServingTicket = 5,
        TotalWaiting = 3,
        WaitingTickets = new List<QueueTicket>
        {
            new() { Id = Guid.NewGuid(), DoctorId = doctorId, TicketNumber = 6, EstimatedTime = DateTime.UtcNow.AddMinutes(10), Status = "Waiting" },
            new() { Id = Guid.NewGuid(), DoctorId = doctorId, TicketNumber = 7, EstimatedTime = DateTime.UtcNow.AddMinutes(25), Status = "Waiting" },
            new() { Id = Guid.NewGuid(), DoctorId = doctorId, TicketNumber = 8, EstimatedTime = DateTime.UtcNow.AddMinutes(40), Status = "Waiting" }
        },
        LastUpdated = DateTime.UtcNow
    };

    return Results.Ok(mockQueueStatus);
});

app.Run();
