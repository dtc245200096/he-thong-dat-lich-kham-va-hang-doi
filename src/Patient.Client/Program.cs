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
    Console.WriteLine("2. Xem khung giờ khám của bác sĩ (GET /doctors/{id}/slots)");
    Console.WriteLine("3. Đặt lịch khám bệnh (POST /appointments)");
    Console.WriteLine("4. Tra cứu thông tin lịch hẹn (GET /appointments/{id})");
    Console.WriteLine("5. Xem trạng thái hàng đợi phòng khám (GET /queues/{doctorId})");
    Console.WriteLine("6. Kiểm tra kết nối Health Check các dịch vụ");
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
                await ShowDoctorSlotsAsync(httpClient);
                break;
            case "3":
                await BookAppointmentAsync(httpClient);
                break;
            case "4":
                await LookupAppointmentAsync(httpClient);
                break;
            case "5":
                await ShowQueueStatusAsync(httpClient);
                break;
            case "6":
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
        Console.WriteLine($"[Lỗi kết nối HTTP] Không thể kết nối tới Appointment.Api ({appointmentApiUrl}).");
        Console.WriteLine($"Chi tiết: {ex.Message}");
        Console.WriteLine("Gợi ý: Hãy đảm bảo dịch vụ Appointment.Api đang chạy hoặc qua Docker Compose.");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[Lỗi] {ex.Message}");
        Console.ResetColor();
    }
}

static async Task ShowDoctorsAsync(HttpClient client)
{
    Console.WriteLine(">>> Đang gửi yêu cầu GET /doctors...");
    var response = await client.GetAsync("/doctors");
    response.EnsureSuccessStatusCode();

    var doctors = await response.Content.ReadFromJsonAsync<List<Doctor>>();
    Console.WriteLine("\n--- DANH SÁCH BÁC SĨ ---");
    if (doctors is null || doctors.Count == 0)
    {
        Console.WriteLine("Chưa có bác sĩ nào trong hệ thống.");
        return;
    }

    foreach (var doc in doctors)
    {
        Console.WriteLine($"- ID: {doc.Id} | {doc.FullName} | Chuyên khoa: {doc.Specialty} | {doc.RoomNumber}");
    }
}

static async Task ShowDoctorSlotsAsync(HttpClient client)
{
    Console.Write("Nhập Doctor ID (bỏ trống để dùng ID mặc định BS. Nguyễn Văn A): ");
    var input = Console.ReadLine()?.Trim();
    var doctorId = string.IsNullOrEmpty(input) ? "11111111-1111-1111-1111-111111111111" : input;

    Console.WriteLine($">>> Đang gửi yêu cầu GET /doctors/{doctorId}/slots...");
    var response = await client.GetAsync($"/doctors/{doctorId}/slots");
    response.EnsureSuccessStatusCode();

    var slots = await response.Content.ReadFromJsonAsync<List<TimeSlot>>();
    Console.WriteLine("\n--- KHUNG GIỜ KHÁM ---");
    if (slots is null || slots.Count == 0)
    {
        Console.WriteLine("Bác sĩ không có khung giờ trống.");
        return;
    }

    foreach (var s in slots)
    {
        var status = s.IsBooked ? "[Đã đặt]" : "[Còn trống]";
        Console.WriteLine($"- Slot ID: {s.Id} | {s.StartTime:HH:mm} - {s.EndTime:HH:mm} {status}");
    }
}

static async Task BookAppointmentAsync(HttpClient client)
{
    Console.WriteLine(">>> Điền thông tin đặt lịch khám:");
    var patientId = Guid.NewGuid();
    var doctorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    var slotId = Guid.NewGuid();

    Console.Write("Nhập lý do khám (mặc định 'Đau họng, sốt nhẹ'): ");
    var reasonInput = Console.ReadLine()?.Trim();
    var reason = string.IsNullOrEmpty(reasonInput) ? "Đau họng, sốt nhẹ" : reasonInput;

    var request = new CreateAppointmentRequest
    {
        PatientId = patientId,
        DoctorId = doctorId,
        TimeSlotId = slotId,
        AppointmentDate = DateTime.UtcNow.Date.AddHours(9),
        Reason = reason
    };

    Console.WriteLine(">>> Đang gửi yêu cầu POST /appointments...");
    var response = await client.PostAsJsonAsync("/appointments", request);
    var content = await response.Content.ReadAsStringAsync();

    Console.WriteLine($"\nKết quả từ Server (Status {response.StatusCode}):");
    Console.WriteLine(content);
}

static async Task LookupAppointmentAsync(HttpClient client)
{
    Console.Write("Nhập Appointment ID (bỏ trống để dùng ID ngẫu nhiên): ");
    var input = Console.ReadLine()?.Trim();
    var id = string.IsNullOrEmpty(input) ? Guid.NewGuid().ToString() : input;

    Console.WriteLine($">>> Đang gửi yêu cầu GET /appointments/{id}...");
    var response = await client.GetAsync($"/appointments/{id}");
    var content = await response.Content.ReadAsStringAsync();

    Console.WriteLine($"\nKết quả từ Server (Status {response.StatusCode}):");
    Console.WriteLine(content);
}

static async Task ShowQueueStatusAsync(HttpClient client)
{
    Console.Write("Nhập Doctor ID (bỏ trống để dùng ID mặc định BS. Nguyễn Văn A): ");
    var input = Console.ReadLine()?.Trim();
    var doctorId = string.IsNullOrEmpty(input) ? "11111111-1111-1111-1111-111111111111" : input;

    Console.WriteLine($">>> Đang gửi yêu cầu GET /queues/{doctorId}...");
    var response = await client.GetAsync($"/queues/{doctorId}");
    var content = await response.Content.ReadAsStringAsync();

    Console.WriteLine($"\nTrạng thái hàng đợi (Status {response.StatusCode}):");
    Console.WriteLine(content);
}

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
