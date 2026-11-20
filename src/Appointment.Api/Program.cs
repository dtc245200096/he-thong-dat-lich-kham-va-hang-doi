using System.Collections.Concurrent;
using Appointment.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Shared.Contracts.DTOs;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;
using AppointmentModel = Shared.Contracts.Models.Appointment;

var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// CẤU HÌNH HỆ THỐNG & ĐỌC BIẾN MÔI TRƯỜNG / APPSETTINGS
// =============================================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var rabbitMqHost = builder.Configuration["RabbitMQ:HostName"] ?? "localhost";
var rabbitMqPort = builder.Configuration.GetValue<int>("RabbitMQ:Port", 5672);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Đăng ký SignalR Hub trực tiếp trên API Server để hỗ trợ kết nối realtime
builder.Services.AddSignalR();

// Cấu hình CORS cho Client kết nối qua HTTP & WebSocket
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials();
    });
});

var app = builder.Build();

var logger = app.Logger;
logger.LogInformation(">>> [Appointment.Api] Starting service...");
logger.LogInformation(">>> Database Connection: {ConnectionString}", 
    string.IsNullOrEmpty(connectionString) ? "Not Configured" : "Configured from Environment/Config");
logger.LogInformation(">>> RabbitMQ Broker: {Host}:{Port}", rabbitMqHost, rabbitMqPort);

app.UseCors();

// =============================================================================
// DỮ LIỆU MẪU (MOCK IN-MEMORY DATA STORE)
// =============================================================================
var doctors = new List<Doctor>
{
    new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), FullName = "BS. Nguyễn Văn A", Specialty = "Nội tổng quát", RoomNumber = "Phòng 101", IsActive = true },
    new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), FullName = "BS. Trần Thị B", Specialty = "Tim mạch", RoomNumber = "Phòng 102", IsActive = true },
    new() { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), FullName = "BS. Lê Văn C", Specialty = "Nhi khoa", RoomNumber = "Phòng 103", IsActive = true },
    new() { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), FullName = "BS. Phạm Thị D", Specialty = "Da liễu", RoomNumber = "Phòng 104", IsActive = true }
};

var today = DateTime.Today;
var slots = new List<TimeSlot>
{
    // Khung giờ BS. Nguyễn Văn A
    new() { Id = Guid.Parse("a1111111-0000-0000-0000-000000000001"), DoctorId = Guid.Parse("11111111-1111-1111-1111-111111111111"), StartTime = today.AddHours(8), EndTime = today.AddHours(8).AddMinutes(30), IsBooked = false },
    new() { Id = Guid.Parse("a1111111-0000-0000-0000-000000000002"), DoctorId = Guid.Parse("11111111-1111-1111-1111-111111111111"), StartTime = today.AddHours(8).AddMinutes(30), EndTime = today.AddHours(9), IsBooked = false },
    new() { Id = Guid.Parse("a1111111-0000-0000-0000-000000000003"), DoctorId = Guid.Parse("11111111-1111-1111-1111-111111111111"), StartTime = today.AddHours(9), EndTime = today.AddHours(9).AddMinutes(30), IsBooked = true },

    // Khung giờ BS. Trần Thị B
    new() { Id = Guid.Parse("b2222222-0000-0000-0000-000000000001"), DoctorId = Guid.Parse("22222222-2222-2222-2222-222222222222"), StartTime = today.AddHours(8), EndTime = today.AddHours(8).AddMinutes(30), IsBooked = false },
    new() { Id = Guid.Parse("b2222222-0000-0000-0000-000000000002"), DoctorId = Guid.Parse("22222222-2222-2222-2222-222222222222"), StartTime = today.AddHours(8).AddMinutes(30), EndTime = today.AddHours(9), IsBooked = false },

    // Khung giờ BS. Lê Văn C
    new() { Id = Guid.Parse("c3333333-0000-0000-0000-000000000001"), DoctorId = Guid.Parse("33333333-3333-3333-3333-333333333333"), StartTime = today.AddHours(9), EndTime = today.AddHours(9).AddMinutes(30), IsBooked = false },

    // Khung giờ BS. Phạm Thị D
    new() { Id = Guid.Parse("d4444444-0000-0000-0000-000000000001"), DoctorId = Guid.Parse("44444444-4444-4444-4444-444444444444"), StartTime = today.AddHours(13).AddMinutes(30), EndTime = today.AddHours(14), IsBooked = false },
    new() { Id = Guid.Parse("d4444444-0000-0000-0000-000000000002"), DoctorId = Guid.Parse("44444444-4444-4444-4444-444444444444"), StartTime = today.AddHours(14), EndTime = today.AddHours(14).AddMinutes(30), IsBooked = false }
};

var appointments = new ConcurrentDictionary<Guid, AppointmentModel>();
int globalTicketSequence = 10; // Bắt đầu từ số 10 để mô phỏng hàng đợi đang hoạt động

// Khởi tạo một lịch hẹn mẫu
var sampleAppointmentId = Guid.Parse("99999999-9999-9999-9999-999999999999");
appointments.TryAdd(sampleAppointmentId, new AppointmentModel
{
    Id = sampleAppointmentId,
    PatientId = Guid.Parse("88888888-8888-8888-8888-888888888888"),
    DoctorId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
    TimeSlotId = Guid.Parse("a1111111-0000-0000-0000-000000000003"),
    AppointmentDate = today.AddHours(9),
    Status = AppointmentStatus.Confirmed,
    Reason = "Tái khám định kỳ tăng huyết áp",
    CreatedAt = DateTime.UtcNow.AddMinutes(-30)
});

// =============================================================================
// ROOT & SIGNALR HUB REGISTRATION
// =============================================================================
app.MapGet("/", () => Results.Ok(new
{
    Service = "Appointment.Api",
    Status = "Healthy",
    SignalREndpoint = "/hubs/queue",
    Version = "1.0.0",
    Timestamp = DateTime.UtcNow
}));

// Đăng ký SignalR Hub tại endpoint /hubs/queue
app.MapHub<QueueHub>("/hubs/queue");

// =============================================================================
// CÁC REST ENDPOINTS
// =============================================================================

// 1. GET /doctors
app.MapGet("/doctors", (ILogger<Program> log) =>
{
    log.LogInformation("[GET /doctors] Fetching active doctors list (Total: {Count})", doctors.Count);
    return Results.Ok(doctors);
});

// 2. GET /doctors/{id}
app.MapGet("/doctors/{id:guid}", (Guid id, ILogger<Program> log) =>
{
    log.LogInformation("[GET /doctors/{Id}] Fetching doctor details", id);
    var doctor = doctors.FirstOrDefault(d => d.Id == id);
    if (doctor is null)
    {
        return Results.NotFound(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status404NotFound,
            Message = $"Không tìm thấy thông tin bác sĩ có mã '{id}' trong hệ thống.",
            Details = "Vui lòng kiểm tra lại ID bác sĩ hoặc gọi GET /doctors để xem danh sách."
        });
    }

    return Results.Ok(doctor);
});

// 3. GET /doctors/{id}/slots
app.MapGet("/doctors/{id:guid}/slots", (Guid id, ILogger<Program> log) =>
{
    log.LogInformation("[GET /doctors/{DoctorId}/slots] Fetching available slots", id);
    var doctor = doctors.FirstOrDefault(d => d.Id == id);
    if (doctor is null)
    {
        return Results.NotFound(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status404NotFound,
            Message = $"Không tìm thấy bác sĩ có mã '{id}'. Không thể lấy danh sách khung giờ khám."
        });
    }

    var doctorSlots = slots.Where(s => s.DoctorId == id).ToList();
    return Results.Ok(doctorSlots);
});

// 4. POST /appointments - Tiếp nhận đặt lịch & Kích hoạt tiến trình xếp hàng thời gian thực
app.MapPost("/appointments", (CreateAppointmentRequest request, IHubContext<QueueHub> hubContext, ILogger<Program> log) =>
{
    log.LogInformation("[POST /appointments] Receiving appointment booking request");

    // Validation 400 Bad Request
    var validationErrors = new Dictionary<string, string[]>();
    if (request.PatientId == Guid.Empty) validationErrors.Add(nameof(request.PatientId), ["Mã bệnh nhân không được để trống."]);
    if (request.DoctorId == Guid.Empty) validationErrors.Add(nameof(request.DoctorId), ["Mã bác sĩ không được để trống."]);
    if (request.TimeSlotId == Guid.Empty) validationErrors.Add(nameof(request.TimeSlotId), ["Khung giờ khám không được để trống."]);
    if (string.IsNullOrWhiteSpace(request.Reason)) validationErrors.Add(nameof(request.Reason), ["Lý do khám không được để trống."]);

    if (validationErrors.Count > 0)
    {
        return Results.BadRequest(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Message = "Dữ liệu yêu cầu đặt lịch khám không hợp lệ.",
            ValidationErrors = validationErrors
        });
    }

    // Kiểm tra bác sĩ & khung giờ (404)
    var doctor = doctors.FirstOrDefault(d => d.Id == request.DoctorId);
    if (doctor is null)
    {
        return Results.NotFound(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status404NotFound,
            Message = $"Bác sĩ có mã '{request.DoctorId}' không tồn tại trong hệ thống."
        });
    }

    var slot = slots.FirstOrDefault(s => s.Id == request.TimeSlotId && s.DoctorId == request.DoctorId);
    if (slot is null)
    {
        return Results.NotFound(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status404NotFound,
            Message = $"Khung giờ '{request.TimeSlotId}' không thuộc về bác sĩ này hoặc không tồn tại."
        });
    }

    if (slot.IsBooked)
    {
        return Results.BadRequest(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Message = $"Khung giờ {slot.StartTime:HH:mm} - {slot.EndTime:HH:mm} đã có bệnh nhân đặt trước."
        });
    }

    // Tạo lịch hẹn với trạng thái khởi tạo: Pending
    var appointment = new AppointmentModel
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

    appointments.TryAdd(appointment.Id, appointment);

    // =========================================================================
    // MÔ PHỎNG TIẾN TRÌNH XỬ LÝ HÀNG ĐỢI HẬU TRƯỜNG & SIGNALR REALTIME (Yêu cầu 1, 2, 3)
    // Tương đương với việc bắn sự kiện lên RabbitMQ -> Queue.Worker tiêu thụ -> phát SignalR
    // =========================================================================
    _ = Task.Run(async () =>
    {
        try
        {
            // Giả lập độ trễ hàng đợi 2.5 giây
            await Task.Delay(2500);

            // Kiểm tra kịch bản lỗi 'failed' (Yêu cầu 2):
            // Nếu lý do khám chứa "fail", "loi", "error", giả lập tình huống khung giờ đầy đột xuất hoặc lỗi xử lý
            var reason = request.Reason ?? string.Empty;
            bool shouldFail = reason.Contains("fail", StringComparison.OrdinalIgnoreCase) ||
                              reason.Contains("loi", StringComparison.OrdinalIgnoreCase) ||
                              reason.Contains("error", StringComparison.OrdinalIgnoreCase);

            if (shouldFail)
            {
                var failedAppointment = appointment with 
                { 
                    Status = AppointmentStatus.Failed, 
                    Reason = $"{appointment.Reason} [LÝ DO LỖI: Khung giờ đầy đột xuất do tiếp nhận ca cấp cứu ưu tiên]",
                    UpdatedAt = DateTime.UtcNow 
                };
                appointments[appointment.Id] = failedAppointment;

                log.LogWarning("[Queue Background Worker] Lịch hẹn {AppointmentId} xử lý THẤT BẠI (Status: Failed)", appointment.Id);

                // Phát thông báo FAILED tới đúng SignalR Group của lịch hẹn (Isolation - Yêu cầu 3)
                await hubContext.Clients.Group($"Appointment_{appointment.Id}").SendAsync("ReceiveAppointmentStatus", new AppointmentStatusNotification
                {
                    AppointmentId = appointment.Id,
                    PatientId = appointment.PatientId,
                    DoctorId = appointment.DoctorId,
                    Status = AppointmentStatus.Failed,
                    Message = "Xử lý hàng đợi THẤT BẠI: Khung giờ khám đã đầy đột xuất do ca cấp cứu ưu tiên.",
                    Timestamp = DateTime.UtcNow
                });
            }
            else
            {
                // Cấp số thứ tự (Ticket Number)
                var ticketNumber = Interlocked.Increment(ref globalTicketSequence);
                var confirmedAppointment = appointment with 
                { 
                    Status = AppointmentStatus.Confirmed, 
                    UpdatedAt = DateTime.UtcNow 
                };
                appointments[appointment.Id] = confirmedAppointment;

                // Đánh dấu slot đã đặt
                var slotIndex = slots.FindIndex(s => s.Id == slot.Id);
                if (slotIndex >= 0)
                {
                    slots[slotIndex] = slot with { IsBooked = true };
                }

                log.LogInformation("[Queue Background Worker] Lịch hẹn {AppointmentId} đã XÁC NHẬN! Cấp STT: #{TicketNumber}", 
                    appointment.Id, ticketNumber);

                // Phát thông báo CONFIRMED tới đúng SignalR Group của lịch hẹn (Isolation - Yêu cầu 3)
                await hubContext.Clients.Group($"Appointment_{appointment.Id}").SendAsync("ReceiveAppointmentStatus", new AppointmentStatusNotification
                {
                    AppointmentId = appointment.Id,
                    PatientId = appointment.PatientId,
                    DoctorId = appointment.DoctorId,
                    Status = AppointmentStatus.Confirmed,
                    TicketNumber = ticketNumber,
                    EstimatedTime = DateTime.UtcNow.AddMinutes(ticketNumber * 10),
                    Message = $"Đặt lịch thành công! Số thứ tự khám của bạn là #{ticketNumber} tại {doctor.RoomNumber}.",
                    Timestamp = DateTime.UtcNow
                });

                // Đồng thời phát cập nhật tới màn hình chờ của bác sĩ
                await hubContext.Clients.Group($"DoctorQueue_{doctor.Id}").SendAsync("ReceiveQueueUpdate", new
                {
                    DoctorId = doctor.Id,
                    DoctorName = doctor.FullName,
                    RoomNumber = doctor.RoomNumber,
                    LatestTicketNumber = ticketNumber,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Lỗi trong tiến trình mô phỏng hàng đợi");
        }
    });

    return Results.Created($"/appointments/{appointment.Id}", new
    {
        Message = "Yêu cầu đặt lịch khám đã được tiếp nhận thành công. Vui lòng lắng nghe số thứ tự qua SignalR.",
        Appointment = appointment
    });
});

// 5. GET /appointments/{id} - Tra cứu chi tiết một lịch hẹn (Dùng đối chiếu trạng thái cuối - Yêu cầu 1)
app.MapGet("/appointments/{id:guid}", (Guid id, ILogger<Program> log) =>
{
    log.LogInformation("[GET /appointments/{Id}] Looking up appointment details", id);

    if (appointments.TryGetValue(id, out var appointment))
    {
        var doctor = doctors.FirstOrDefault(d => d.Id == appointment.DoctorId);
        return Results.Ok(new
        {
            Appointment = appointment,
            DoctorDetails = doctor is null ? null : new
            {
                doctor.FullName,
                doctor.Specialty,
                doctor.RoomNumber
            }
        });
    }

    return Results.NotFound(new ApiErrorResponse
    {
        StatusCode = StatusCodes.Status404NotFound,
        Message = $"Không tìm thấy lịch hẹn khám có mã '{id}' trong hệ thống."
    });
});

// 6. DELETE /appointments/{id}
app.MapDelete("/appointments/{id:guid}", (Guid id, ILogger<Program> log) =>
{
    log.LogInformation("[DELETE /appointments/{Id}] Request to cancel appointment", id);

    if (appointments.TryGetValue(id, out var appointment))
    {
        var updated = appointment with { Status = AppointmentStatus.Cancelled, UpdatedAt = DateTime.UtcNow };
        appointments[id] = updated;

        return Results.Ok(new
        {
            Message = $"Lịch hẹn {id} đã được hủy thành công.",
            AppointmentId = id,
            Status = AppointmentStatus.Cancelled
        });
    }

    return Results.NotFound(new ApiErrorResponse
    {
        StatusCode = StatusCodes.Status404NotFound,
        Message = $"Không thể hủy. Lịch hẹn có mã '{id}' không tồn tại."
    });
});

// 7. GET /queues/{doctorId}
app.MapGet("/queues/{doctorId:guid}", (Guid doctorId, ILogger<Program> log) =>
{
    log.LogInformation("[GET /queues/{DoctorId}] Getting queue status", doctorId);

    var doctor = doctors.FirstOrDefault(d => d.Id == doctorId);
    if (doctor is null)
    {
        return Results.NotFound(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status404NotFound,
            Message = $"Không tìm thấy bác sĩ có mã '{doctorId}'."
        });
    }

    var mockQueueStatus = new QueueStatusResponse
    {
        DoctorId = doctorId,
        DoctorName = doctor.FullName,
        RoomNumber = doctor.RoomNumber,
        CurrentServingTicket = 10,
        TotalWaiting = 2,
        WaitingTickets = new List<QueueTicket>
        {
            new() { Id = Guid.NewGuid(), DoctorId = doctorId, TicketNumber = 11, EstimatedTime = DateTime.UtcNow.AddMinutes(10), Status = "Waiting" },
            new() { Id = Guid.NewGuid(), DoctorId = doctorId, TicketNumber = 12, EstimatedTime = DateTime.UtcNow.AddMinutes(25), Status = "Waiting" }
        },
        LastUpdated = DateTime.UtcNow
    };

    return Results.Ok(mockQueueStatus);
});

app.Run();
