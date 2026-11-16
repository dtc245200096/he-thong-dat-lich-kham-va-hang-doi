using System.Collections.Concurrent;
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
// DỮ LIỆU MẪU (MOCK IN-MEMORY DATA STORE) - SẴN SÀNG CHO PHASE 1 & 2
// =============================================================================
var doctors = new List<Doctor>
{
    new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), FullName = "BS. Nguyễn Văn A", Specialty = "Nội tổng quát", RoomNumber = "Phòng 101", IsActive = true },
    new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), FullName = "BS. Trần Thị B", Specialty = "Tim mạch", RoomNumber = "Phòng 102", IsActive = true },
    new() { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), FullName = "BS. Lê Văn C", Specialty = "Nhi khoa", RoomNumber = "Phòng 103", IsActive = true },
    // Dữ liệu mẫu mới bổ sung (Yêu cầu 2)
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

    // Khung giờ mẫu mới cho BS. Phạm Thị D (Yêu cầu 2)
    new() { Id = Guid.Parse("d4444444-0000-0000-0000-000000000001"), DoctorId = Guid.Parse("44444444-4444-4444-4444-444444444444"), StartTime = today.AddHours(13).AddMinutes(30), EndTime = today.AddHours(14), IsBooked = false },
    new() { Id = Guid.Parse("d4444444-0000-0000-0000-000000000002"), DoctorId = Guid.Parse("44444444-4444-4444-4444-444444444444"), StartTime = today.AddHours(14), EndTime = today.AddHours(14).AddMinutes(30), IsBooked = false }
};

// Kho lưu trữ lịch hẹn tạm thời in-memory
var appointments = new ConcurrentDictionary<Guid, Appointment>();

// Khởi tạo sẵn một lịch hẹn mẫu hợp lệ
var sampleAppointmentId = Guid.Parse("99999999-9999-9999-9999-999999999999");
appointments.TryAdd(sampleAppointmentId, new Appointment
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
// CÁC ENDPOINT MINIMAL API
// =============================================================================

// 1. GET /doctors - Lấy danh sách bác sĩ đang hoạt động
app.MapGet("/doctors", (ILogger<Program> log) =>
{
    log.LogInformation("[GET /doctors] Fetching active doctors list (Total: {Count})", doctors.Count);
    return Results.Ok(doctors);
});

// 2. GET /doctors/{id} - Lấy thông tin chi tiết một bác sĩ (Yêu cầu 3 & 4)
app.MapGet("/doctors/{id:guid}", (Guid id, ILogger<Program> log) =>
{
    log.LogInformation("[GET /doctors/{Id}] Fetching doctor details", id);
    var doctor = doctors.FirstOrDefault(d => d.Id == id);
    if (doctor is null)
    {
        log.LogWarning("[GET /doctors/{Id}] Doctor not found", id);
        return Results.NotFound(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status404NotFound,
            Message = $"Không tìm thấy thông tin bác sĩ có mã '{id}' trong hệ thống.",
            Details = "Vui lòng kiểm tra lại ID bác sĩ hoặc gọi GET /doctors để xem danh sách bác sĩ hiện có."
        });
    }

    return Results.Ok(doctor);
});

// 3. GET /doctors/{id}/slots - Lấy danh sách khung giờ khám của bác sĩ (Yêu cầu 4)
app.MapGet("/doctors/{id:guid}/slots", (Guid id, ILogger<Program> log) =>
{
    log.LogInformation("[GET /doctors/{DoctorId}/slots] Fetching available slots", id);

    var doctor = doctors.FirstOrDefault(d => d.Id == id);
    if (doctor is null)
    {
        log.LogWarning("[GET /doctors/{DoctorId}/slots] Doctor not found", id);
        return Results.NotFound(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status404NotFound,
            Message = $"Không tìm thấy bác sĩ có mã '{id}'. Không thể lấy danh sách khung giờ khám.",
            Details = "Bác sĩ không tồn tại hoặc đã ngừng hoạt động."
        });
    }

    var doctorSlots = slots.Where(s => s.DoctorId == id).ToList();
    return Results.Ok(doctorSlots);
});

// 4. POST /appointments - Tiếp nhận yêu cầu đặt lịch khám (Validate 400 & 404 - Yêu cầu 1)
app.MapPost("/appointments", (CreateAppointmentRequest request, ILogger<Program> log) =>
{
    log.LogInformation("[POST /appointments] Receiving appointment booking request");

    // --- BƯỚC VALIDATION DỮ LIỆU ĐẦU VÀO (HTTP 400 Bad Request) ---
    var validationErrors = new Dictionary<string, string[]>();

    if (request.PatientId == Guid.Empty)
    {
        validationErrors.Add(nameof(request.PatientId), ["Mã bệnh nhân (PatientId) không được để trống."]);
    }

    if (request.DoctorId == Guid.Empty)
    {
        validationErrors.Add(nameof(request.DoctorId), ["Mã bác sĩ (DoctorId) không được để trống."]);
    }

    if (request.TimeSlotId == Guid.Empty)
    {
        validationErrors.Add(nameof(request.TimeSlotId), ["Khung giờ khám (TimeSlotId) không được để trống."]);
    }

    if (string.IsNullOrWhiteSpace(request.Reason))
    {
        validationErrors.Add(nameof(request.Reason), ["Lý do khám bệnh không được để trống."]);
    }

    if (validationErrors.Count > 0)
    {
        log.LogWarning("[POST /appointments] Validation failed with {Count} errors", validationErrors.Count);
        return Results.BadRequest(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Message = "Dữ liệu yêu cầu đặt lịch khám không hợp lệ.",
            Details = "Vui lòng kiểm tra lại các trường thông tin theo danh sách lỗi chi tiết kèm theo.",
            ValidationErrors = validationErrors
        });
    }

    // --- KIỂM TRA TỒN TẠI BÁC SĨ (HTTP 404 Not Found) ---
    var doctor = doctors.FirstOrDefault(d => d.Id == request.DoctorId);
    if (doctor is null)
    {
        return Results.NotFound(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status404NotFound,
            Message = $"Bác sĩ có mã '{request.DoctorId}' không tồn tại trong hệ thống."
        });
    }

    // --- KIỂM TRA KHUNG GIỜ KHÁM ---
    var slot = slots.FirstOrDefault(s => s.Id == request.TimeSlotId && s.DoctorId == request.DoctorId);
    if (slot is null)
    {
        return Results.NotFound(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status404NotFound,
            Message = $"Khung giờ khám '{request.TimeSlotId}' không thuộc về bác sĩ '{doctor.FullName}' hoặc không tồn tại."
        });
    }

    if (slot.IsBooked)
    {
        return Results.BadRequest(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Message = $"Khung giờ {slot.StartTime:HH:mm} - {slot.EndTime:HH:mm} đã có bệnh nhân khác đặt trước. Vui lòng chọn khung giờ khác."
        });
    }

    // --- TẠO VÀ LƯU LỊCH HẸN ---
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

    appointments.TryAdd(appointment.Id, appointment);

    // Đánh dấu slot đã được đặt (mock)
    var slotIndex = slots.FindIndex(s => s.Id == slot.Id);
    if (slotIndex >= 0)
    {
        slots[slotIndex] = slot with { IsBooked = true };
    }

    // TODO: Phase 3 - Bắn sự kiện AppointmentCreatedEvent lên RabbitMQ (clinic.appointment.exchange)
    log.LogInformation("[RabbitMQ Placeholder] Will publish AppointmentCreatedEvent for AppointmentId {AppointmentId}", appointment.Id);

    return Results.Created($"/appointments/{appointment.Id}", new
    {
        Message = "Yêu cầu đặt lịch khám đã được tiếp nhận thành công và đang chờ cấp số thứ tự hàng đợi.",
        Appointment = appointment
    });
});

// 5. GET /appointments/{id} - Tra cứu chi tiết một lịch hẹn (Yêu cầu 3 & 4)
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

    log.LogWarning("[GET /appointments/{Id}] Appointment not found", id);
    return Results.NotFound(new ApiErrorResponse
    {
        StatusCode = StatusCodes.Status404NotFound,
        Message = $"Không tìm thấy lịch hẹn khám có mã '{id}' trong hệ thống.",
        Details = "Lịch hẹn có thể chưa được tạo hoặc đã bị xóa. Vui lòng kiểm tra lại mã số lịch hẹn."
    });
});

// 6. DELETE /appointments/{id} - Hủy lịch hẹn khám
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

// 7. GET /queues/{doctorId} - Lấy trạng thái hàng đợi hiện tại của phòng khám/bác sĩ
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
