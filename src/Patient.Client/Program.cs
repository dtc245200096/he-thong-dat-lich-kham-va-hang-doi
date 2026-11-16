using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Shared.Contracts.DTOs;
using Shared.Contracts.Models;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

var appointmentApiUrl = configuration["ServiceUrls:AppointmentApi"] ?? "http://localhost:5001";
var notificationServiceUrl = configuration["ServiceUrls:NotificationService"] ?? "http://localhost:5003";

using var httpClient = new HttpClient { BaseAddress = new Uri(appointmentApiUrl) };

Console.WriteLine("================================================================================");
Console.WriteLine("         HỆ THỐNG ĐẶT LỊCH KHÁM VÀ QUẢN LÝ HÀNG ĐỢI (ĐỀ TÀI 09)");
Console.WriteLine("                         PATIENT CLIENT APPLICATION");
Console.WriteLine("================================================================================");
Console.WriteLine($"[Config] Appointment.Api:        {appointmentApiUrl}");
Console.WriteLine($"[Config] Notification.Service:   {notificationServiceUrl}");
Console.WriteLine("--------------------------------------------------------------------------------");

bool running = true;
while (running)
{
    Console.WriteLine("\nCHỌN CHỨC NĂNG:");
    Console.WriteLine("1. Xem danh sách bác sĩ (GET /doctors)");
    Console.WriteLine("2. Xem chi tiết bác sĩ theo ID (GET /doctors/{id}) - [Yêu cầu 3 & 4]");
    Console.WriteLine("3. Xem khung giờ khám của bác sĩ (GET /doctors/{id}/slots)");
    Console.WriteLine("4. Đặt lịch khám bệnh (POST /appointments) - [Yêu cầu 1]");
    Console.WriteLine("5. Tra cứu chi tiết lịch hẹn (GET /appointments/{id}) - [Yêu cầu 3 & 4]");
    Console.WriteLine("6. Hủy lịch hẹn khám (DELETE /appointments/{id})");
    Console.WriteLine("7. Xem trạng thái hàng đợi phòng khám (GET /queues/{doctorId})");
    Console.WriteLine("8. Kiểm tra kết nối Health Check các dịch vụ");
    Console.WriteLine("0. Thoát");
    Console.Write("\nNhập lựa chọn của bạn: ");

    var choice = Console.ReadLine()?.Trim();
    Console.WriteLine();

    try
    {
        switch (choice)
        {
            case "1":
                await ShowDoctorsAsync(httpClient);
                break;
            case "2":
                await ShowDoctorDetailsAsync(httpClient);
                break;
            case "3":
                await ShowDoctorSlotsAsync(httpClient);
                break;
            case "4":
                await BookAppointmentAsync(httpClient);
                break;
            case "5":
                await LookupAppointmentAsync(httpClient);
                break;
            case "6":
                await CancelAppointmentAsync(httpClient);
                break;
            case "7":
                await ShowQueueStatusAsync(httpClient);
                break;
            case "8":
                await CheckServicesHealthAsync(httpClient, notificationServiceUrl);
                break;
            case "0":
                running = false;
                Console.WriteLine("Cảm ơn bạn đã sử dụng dịch vụ!");
                break;
            default:
                Console.WriteLine("Lựa chọn không hợp lệ. Vui lòng chọn lại.");
                break;
        }
    }
    catch (HttpRequestException ex)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[Lỗi mạng] Không thể gửi yêu cầu tới Appointment.Api ({appointmentApiUrl}).");
        Console.WriteLine($"Chi tiết: {ex.Message}");
        Console.WriteLine("Gợi ý: Hãy đảm bảo dịch vụ Appointment.Api đang chạy (ví dụ qua lệnh 'dotnet run' hoặc 'docker compose up').");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[Lỗi ngoại lệ] {ex.Message}");
        Console.ResetColor();
    }
}

// =============================================================================
// CÁC HÀM XỬ LÝ NGHIỆP VỤ & GỌI API PHÍA CLIENT
// =============================================================================

// 1. GET /doctors
static async Task ShowDoctorsAsync(HttpClient client)
{
    Console.WriteLine(">>> Đang gửi yêu cầu: GET /doctors ...");
    var response = await client.GetAsync("/doctors");

    if (!response.IsSuccessStatusCode)
    {
        await HandleApiErrorAsync(response);
        return;
    }

    var doctors = await response.Content.ReadFromJsonAsync<List<Doctor>>();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n--- DANH SÁCH BÁC SĨ (Tổng số: {doctors?.Count ?? 0}) ---");
    Console.ResetColor();

    if (doctors is null || doctors.Count == 0)
    {
        Console.WriteLine("Chưa có bác sĩ nào trong hệ thống.");
        return;
    }

    foreach (var doc in doctors)
    {
        Console.WriteLine($"• ID: {doc.Id}");
        Console.WriteLine($"  Họ và tên:   {doc.FullName}");
        Console.WriteLine($"  Chuyên khoa: {doc.Specialty} | {doc.RoomNumber}");
        Console.WriteLine($"  Trạng thái:  {(doc.IsActive ? "Đang tiếp nhận khám" : "Tạm nghỉ")}");
        Console.WriteLine(new string('-', 60));
    }
}

// 2. GET /doctors/{id} - Yêu cầu 3 & 4
static async Task ShowDoctorDetailsAsync(HttpClient client)
{
    Console.WriteLine("Nhập Doctor ID cần tra cứu chi tiết:");
    Console.WriteLine("  (1) Bác sĩ mẫu: 11111111-1111-1111-1111-111111111111 (BS. Nguyễn Văn A)");
    Console.WriteLine("  (2) Bác sĩ mới: 44444444-4444-4444-4444-444444444444 (BS. Phạm Thị D)");
    Console.WriteLine("  (3) ID không tồn tại để test lỗi 404: 00000000-0000-0000-0000-000000000404");
    Console.Write("Nhập ID (bỏ trống để dùng ID BS. Phạm Thị D): ");

    var input = Console.ReadLine()?.Trim();
    var doctorId = string.IsNullOrEmpty(input) ? "44444444-4444-4444-4444-444444444444" : input;

    Console.WriteLine($"\n>>> Đang gửi yêu cầu: GET /doctors/{doctorId} ...");
    var response = await client.GetAsync($"/doctors/{doctorId}");

    // Xử lý lỗi chuẩn 404 / 400 thay vì để văng Exception
    if (!response.IsSuccessStatusCode)
    {
        await HandleApiErrorAsync(response);
        return;
    }

    var doctor = await response.Content.ReadFromJsonAsync<Doctor>();
    if (doctor is not null)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n=== THÔNG TIN CHI TIẾT BÁC SĨ ===");
        Console.ResetColor();
        Console.WriteLine($"• Mã định danh (ID): {doctor.Id}");
        Console.WriteLine($"• Họ và tên bác sĩ:  {doctor.FullName}");
        Console.WriteLine($"• Chuyên khoa:       {doctor.Specialty}");
        Console.WriteLine($"• Phòng khám:        {doctor.RoomNumber}");
        Console.WriteLine($"• Tình trạng:        {(doctor.IsActive ? "Đang hoạt động" : "Ngừng tiếp nhận")}");
    }
}

// 3. GET /doctors/{id}/slots
static async Task ShowDoctorSlotsAsync(HttpClient client)
{
    Console.Write("Nhập Doctor ID (bỏ trống để dùng BS. Nguyễn Văn A): ");
    var input = Console.ReadLine()?.Trim();
    var doctorId = string.IsNullOrEmpty(input) ? "11111111-1111-1111-1111-111111111111" : input;

    Console.WriteLine($"\n>>> Đang gửi yêu cầu: GET /doctors/{doctorId}/slots ...");
    var response = await client.GetAsync($"/doctors/{doctorId}/slots");

    if (!response.IsSuccessStatusCode)
    {
        await HandleApiErrorAsync(response);
        return;
    }

    var slots = await response.Content.ReadFromJsonAsync<List<TimeSlot>>();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n--- DANH SÁCH KHUNG GIỜ KHÁM CÒN TRỐNG ---");
    Console.ResetColor();

    if (slots is null || slots.Count == 0)
    {
        Console.WriteLine("Bác sĩ không có khung giờ nào còn khả dụng trong ngày.");
        return;
    }

    foreach (var s in slots)
    {
        var status = s.IsBooked ? "[ĐÃ ĐẶT - HẾT CHỖ]" : "[CÒN TRỐNG - ĐẶT ĐƯỢC]";
        Console.WriteLine($"• Slot ID: {s.Id} | {s.StartTime:HH:mm} - {s.EndTime:HH:mm} -> {status}");
    }
}

// 4. POST /appointments - Yêu cầu 1 (Validate 400 & 404)
static async Task BookAppointmentAsync(HttpClient client)
{
    Console.WriteLine("CHỌN CHẾ ĐỘ ĐẶT LỊCH:");
    Console.WriteLine("  1. Đặt lịch HỢP LỆ (Gửi đầy đủ thông tin chuẩn)");
    Console.WriteLine("  2. Test lỗi 400 Bad Request (Gửi thiếu mã bệnh nhân & lý do)");
    Console.WriteLine("  3. Test lỗi 404 Not Found (Gửi mã bác sĩ không tồn tại)");
    Console.Write("Nhập lựa chọn (1/2/3, mặc định 1): ");
    var mode = Console.ReadLine()?.Trim();

    CreateAppointmentRequest request;

    if (mode == "2")
    {
        // Tạo request thiếu trường bắt buộc để kích hoạt validation 400 Bad Request
        request = new CreateAppointmentRequest
        {
            PatientId = Guid.Empty, // Lỗi
            DoctorId = Guid.Empty,  // Lỗi
            TimeSlotId = Guid.Empty,
            Reason = "" // Lỗi
        };
        Console.WriteLine("\n[Test Mode] Gửi request không hợp lệ (PatientId rỗng, Reason rỗng)...");
    }
    else if (mode == "3")
    {
        // Gửi bác sĩ không tồn tại để kích hoạt 404 Not Found
        request = new CreateAppointmentRequest
        {
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.Parse("00000000-0000-0000-0000-000000000404"),
            TimeSlotId = Guid.NewGuid(),
            Reason = "Khám đau đầu"
        };
        Console.WriteLine("\n[Test Mode] Gửi request với DoctorId không tồn tại trên hệ thống...");
    }
    else
    {
        // Request hợp lệ
        var patientId = Guid.NewGuid();
        var doctorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var slotId = Guid.Parse("a1111111-0000-0000-0000-000000000001");

        Console.Write("Nhập lý do khám (bỏ trống để lấy 'Đau họng, ho kéo dài'): ");
        var reason = Console.ReadLine()?.Trim();

        request = new CreateAppointmentRequest
        {
            PatientId = patientId,
            DoctorId = doctorId,
            TimeSlotId = slotId,
            AppointmentDate = DateTime.UtcNow.Date.AddHours(8),
            Reason = string.IsNullOrEmpty(reason) ? "Đau họng, ho kéo dài" : reason
        };
    }

    Console.WriteLine(">>> Đang gửi yêu cầu: POST /appointments ...");
    var response = await client.PostAsJsonAsync("/appointments", request);

    if (!response.IsSuccessStatusCode)
    {
        await HandleApiErrorAsync(response);
        return;
    }

    var content = await response.Content.ReadAsStringAsync();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n[Thành công - HTTP {response.StatusCode}] Phản hồi từ Server:");
    Console.ResetColor();
    Console.WriteLine(content);
}

// 5. GET /appointments/{id} - Yêu cầu 3 & 4
static async Task LookupAppointmentAsync(HttpClient client)
{
    Console.WriteLine("Tra cứu chi tiết lịch hẹn:");
    Console.WriteLine("  (1) Lịch hẹn mẫu có sẵn: 99999999-9999-9999-9999-999999999999");
    Console.WriteLine("  (2) Mã không tồn tại (để test 404): 00000000-0000-0000-0000-000000000404");
    Console.Write("Nhập Appointment ID (bỏ trống dùng lịch hẹn mẫu): ");

    var input = Console.ReadLine()?.Trim();
    var appointmentId = string.IsNullOrEmpty(input) ? "99999999-9999-9999-9999-999999999999" : input;

    Console.WriteLine($"\n>>> Đang gửi yêu cầu: GET /appointments/{appointmentId} ...");
    var response = await client.GetAsync($"/appointments/{appointmentId}");

    if (!response.IsSuccessStatusCode)
    {
        await HandleApiErrorAsync(response);
        return;
    }

    var content = await response.Content.ReadAsStringAsync();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n=== CHI TIẾT LỊCH HẸN KHÁM BỆNH ===");
    Console.ResetColor();
    Console.WriteLine(content);
}

// 6. DELETE /appointments/{id}
static async Task CancelAppointmentAsync(HttpClient client)
{
    Console.Write("Nhập Appointment ID cần hủy: ");
    var input = Console.ReadLine()?.Trim();
    var appointmentId = string.IsNullOrEmpty(input) ? "99999999-9999-9999-9999-999999999999" : input;

    Console.WriteLine($"\n>>> Đang gửi yêu cầu: DELETE /appointments/{appointmentId} ...");
    var response = await client.DeleteAsync($"/appointments/{appointmentId}");

    if (!response.IsSuccessStatusCode)
    {
        await HandleApiErrorAsync(response);
        return;
    }

    var content = await response.Content.ReadAsStringAsync();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n[Thành công] Phản hồi hủy lịch hẹn:");
    Console.ResetColor();
    Console.WriteLine(content);
}

// 7. GET /queues/{doctorId}
static async Task ShowQueueStatusAsync(HttpClient client)
{
    Console.Write("Nhập Doctor ID (bỏ trống để lấy BS. Nguyễn Văn A): ");
    var input = Console.ReadLine()?.Trim();
    var doctorId = string.IsNullOrEmpty(input) ? "11111111-1111-1111-1111-111111111111" : input;

    Console.WriteLine($"\n>>> Đang gửi yêu cầu: GET /queues/{doctorId} ...");
    var response = await client.GetAsync($"/queues/{doctorId}");

    if (!response.IsSuccessStatusCode)
    {
        await HandleApiErrorAsync(response);
        return;
    }

    var content = await response.Content.ReadAsStringAsync();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n=== TRẠNG THÁI HÀNG ĐỢI PHÒNG KHÁM ===");
    Console.ResetColor();
    Console.WriteLine(content);
}

// 8. Health Check
static async Task CheckServicesHealthAsync(HttpClient apiClient, string notificationUrl)
{
    Console.WriteLine(">>> Kiểm tra trạng thái sức khỏe các dịch vụ:");
    try
    {
        var res = await apiClient.GetAsync("/");
        Console.WriteLine($"[Appointment.Api] Trạng thái: {(res.IsSuccessStatusCode ? "HOẠT ĐỘNG (Healthy)" : "LỖI")}");
    }
    catch
    {
        Console.WriteLine("[Appointment.Api] Không thể kết nối!");
    }

    try
    {
        using var notifClient = new HttpClient { BaseAddress = new Uri(notificationUrl) };
        var res = await notifClient.GetAsync("/");
        Console.WriteLine($"[Notification.Service] Trạng thái: {(res.IsSuccessStatusCode ? "HOẠT ĐỘNG (Healthy)" : "LỖI")}");
    }
    catch
    {
        Console.WriteLine("[Notification.Service] Không thể kết nối!");
    }
}

// =============================================================================
// BỘ XỬ LÝ LỖI CHUẨN DTO CHO CLIENT (Yêu cầu 4)
// Không để crash ứng dụng hay throw ngoại lệ thô, phân tích DTO lỗi trả về từ API
// =============================================================================
static async Task HandleApiErrorAsync(HttpResponseMessage response)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n==================== PHẢN HỒI LỖI TỪ SERVER ====================");
    Console.WriteLine($"Mã trạng thái HTTP: {(int)response.StatusCode} ({response.StatusCode})");

    try
    {
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        if (error is not null)
        {
            Console.WriteLine($"Thông điệp:         {error.Message}");
            if (!string.IsNullOrEmpty(error.Details))
            {
                Console.WriteLine($"Chi tiết:           {error.Details}");
            }

            if (error.ValidationErrors is not null && error.ValidationErrors.Count > 0)
            {
                Console.WriteLine("Danh sách lỗi kiểm tra hợp lệ (Validation Errors):");
                foreach (var (field, errors) in error.ValidationErrors)
                {
                    foreach (var err in errors)
                    {
                        Console.WriteLine($"  - [{field}]: {err}");
                    }
                }
            }
            Console.WriteLine($"Thời điểm ghi nhận: {error.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        }
        else
        {
            var raw = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Nội dung: {raw}");
        }
    }
    catch
    {
        var raw = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Nội dung phản hồi: {raw}");
    }

    Console.WriteLine("================================================================");
    Console.ResetColor();
}
