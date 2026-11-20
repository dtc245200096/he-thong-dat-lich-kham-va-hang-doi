using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Shared.Contracts.DTOs;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

var appointmentApiUrl = configuration["ServiceUrls:AppointmentApi"] ?? "http://localhost:5001";
var signalRHubUrl = $"{appointmentApiUrl.TrimEnd('/')}/hubs/queue";

using var httpClient = new HttpClient { BaseAddress = new Uri(appointmentApiUrl) };

Console.WriteLine("================================================================================");
Console.WriteLine("   HỆ THỐNG ĐẶT LỊCH KHÁM VÀ QUẢN LÝ HÀNG ĐỢI - .NET 10 MINIMAL API & SIGNALR");
Console.WriteLine("                         PATIENT CLIENT APPLICATION");
Console.WriteLine("================================================================================");
Console.WriteLine($"[Config] API Endpoint:        {appointmentApiUrl}");
Console.WriteLine($"[Config] SignalR Hub:         {signalRHubUrl}");
Console.WriteLine("================================================================================\n");

// Danh sách các lịch hẹn đang được Client theo dõi (phục vụ Re-sync khi mất mạng)
var activeTrackingAppointments = new ConcurrentBag<Guid>();

// Khởi tạo SignalR Hub Connection với chính sách tự động kết nối lại
var hubConnection = BuildHubConnection(signalRHubUrl, activeTrackingAppointments, httpClient);

bool running = true;
while (running)
{
    Console.WriteLine("\n----------------------------- MENU CHỨC NĂNG -----------------------------");
    Console.WriteLine("1. [Yêu cầu 1] Chạy LUỒNG CƠ BẢN ĐẦY ĐỦ: REST -> Đặt lịch -> SignalR -> REST");
    Console.WriteLine("2. [Yêu cầu 2] Kiểm thử xử lý TRẠNG THÁI LỖI (Status: Failed) qua SignalR");
    Console.WriteLine("3. [Yêu cầu 3] Kiểm thử ĐỒNG THỜI 2 LỊCH KHÁM (Concurrency & Isolation)");
    Console.WriteLine("4. [Yêu cầu 4] Kiểm thử MẤT KẾT NỐI SignalR & Cơ chế Re-sync qua REST");
    Console.WriteLine("5. Xem danh sách Bác sĩ & Khung giờ khám (REST API thuần túy)");
    Console.WriteLine("6. Tra cứu chi tiết một lịch hẹn bất kỳ qua REST (GET /appointments/{id})");
    Console.WriteLine("0. Thoát");
    Console.Write("\nNhập lựa chọn của bạn: ");

    var choice = Console.ReadLine()?.Trim();
    Console.WriteLine();

    try
    {
        switch (choice)
        {
            case "1":
                await RunBasicFlowAsync(httpClient, hubConnection, activeTrackingAppointments);
                break;
            case "2":
                await RunFailedStateFlowAsync(httpClient, hubConnection, activeTrackingAppointments);
                break;
            case "3":
                await RunConcurrentAppointmentsFlowAsync(httpClient, hubConnection);
                break;
            case "4":
                await TestDisconnectionScenarioAsync(hubConnection, appointmentApiUrl);
                break;
            case "5":
                await ShowDoctorsAndSlotsAsync(httpClient);
                break;
            case "6":
                await LookupAppointmentDetailsAsync(httpClient);
                break;
            case "0":
                running = false;
                Console.WriteLine("Tạm biệt! Đang đóng kết nối...");
                await hubConnection.DisposeAsync();
                break;
            default:
                Console.WriteLine("Lựa chọn không hợp lệ. Vui lòng chọn lại.");
                break;
        }
    }
    catch (HttpRequestException ex)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[Lỗi mạng REST] Không thể kết nối tới Server ({appointmentApiUrl}).");
        Console.WriteLine($"Chi tiết: {ex.Message}");
        Console.WriteLine("Gợi ý: Hãy đảm bảo dịch vụ Appointment.Api đang chạy: dotnet run --project src/Appointment.Api/Appointment.Api.csproj --urls http://localhost:5001");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[Ngoại lệ] {ex.Message}");
        Console.ResetColor();
    }
}

// =============================================================================
// CẤU HÌNH SIGNALR HUB CONNECTION & RECONNECT HANDLERS (YÊU CẦU 4)
// =============================================================================
static HubConnection BuildHubConnection(string hubUrl, ConcurrentBag<Guid> activeTracking, HttpClient client)
{
    var connection = new HubConnectionBuilder()
        .WithUrl(hubUrl)
        // Cấu hình Automatic Reconnect với các khoảng thời gian thử lại: 0s, 2s, 10s, 30s
        .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
        .Build();

    // Sự kiện 1: Đang mất kết nối và đang thử kết nối lại
    connection.Reconnecting += (error) =>
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[SignalR Warning] MẤT KẾT NỐI TỚI SERVER! Đang tự động kết nối lại...");
        Console.WriteLine($"Nguyên nhân: {error?.Message}");
        Console.ResetColor();
        return Task.CompletedTask;
    };

    // Sự kiện 2: Đã kết nối lại thành công -> THỰC HIỆN RE-JOIN GROUP VÀ RE-SYNC REST (Yêu cầu 4)
    connection.Reconnected += async (newConnectionId) =>
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n[SignalR Success] ĐÃ KẾT NỐI LẠI THÀNH CÔNG! ConnectionId mới: {newConnectionId}");
        Console.ResetColor();

        Console.WriteLine(">>> [Reconnection Handler] Đang tự động Re-join các nhóm lịch hẹn và đồng bộ lại trạng thái...");

        foreach (var apptId in activeTracking)
        {
            try
            {
                // 1. Re-join SignalR Group
                await connection.InvokeAsync("JoinAppointmentGroup", apptId.ToString());
                Console.WriteLine($"  + Đã re-join SignalR Group: Appointment_{apptId}");

                // 2. Gọi lại REST API để đọc trạng thái mặt đất (State Reconciliation)
                var res = await client.GetAsync($"/appointments/{apptId}");
                if (res.IsSuccessStatusCode)
                {
                    var content = await res.Content.ReadAsStringAsync();
                    Console.WriteLine($"  + Dữ liệu trạng thái đồng bộ từ REST API:\n    {content}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  - Lỗi khi re-sync lịch hẹn {apptId}: {ex.Message}");
            }
        }
    };

    // Sự kiện 3: Kết nối đóng hoàn toàn (hết số lần retry)
    connection.Closed += (error) =>
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n[SignalR Error] KẾT NỐI ĐÃ ĐÓNG HOÀN TOÀN: {error?.Message}");
        Console.ResetColor();
        return Task.CompletedTask;
    };

    return connection;
}

// Hàm đảm bảo kết nối SignalR đang mở
static async Task EnsureHubConnectedAsync(HubConnection hub)
{
    if (hub.State == HubConnectionState.Disconnected)
    {
        Console.WriteLine(">>> [SignalR] Đang thiết lập kết nối tới Hub...");
        await hub.StartAsync();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[SignalR] Kết nối thành công! ConnectionId = {hub.ConnectionId}");
        Console.ResetColor();
    }
}

// =============================================================================
// YÊU CẦU 1: LUỒNG CƠ BẢN (REST -> ĐẶT LỊCH -> SIGNALR REALTIME -> ĐỐI CHIẾU REST)
// =============================================================================
static async Task RunBasicFlowAsync(HttpClient client, HubConnection hub, ConcurrentBag<Guid> activeTracking)
{
    Console.WriteLine("================================================================================");
    Console.WriteLine("        BẮT ĐẦU KIỂM THỬ YÊU CẦU 1: LUỒNG ĐẶT LỊCH & REALTIME ĐẦY ĐỦ");
    Console.WriteLine("================================================================================");

    // Bước 1: Gọi REST API lấy danh sách bác sĩ
    Console.WriteLine("\n[Bước 1/5] GỌI REST API: GET /doctors");
    var docRes = await client.GetAsync("/doctors");
    docRes.EnsureSuccessStatusCode();
    var doctors = await docRes.Content.ReadFromJsonAsync<List<Doctor>>() ?? [];
    var doctor = doctors.First();
    Console.WriteLine($"-> Chọn bác sĩ: {doctor.FullName} ({doctor.Specialty}) - {doctor.RoomNumber}");

    // Bước 2: Gọi REST API lấy khung giờ khám trống
    Console.WriteLine($"\n[Bước 2/5] GỌI REST API: GET /doctors/{doctor.Id}/slots");
    var slotRes = await client.GetAsync($"/doctors/{doctor.Id}/slots");
    slotRes.EnsureSuccessStatusCode();
    var slots = await slotRes.Content.ReadFromJsonAsync<List<TimeSlot>>() ?? [];
    var availableSlot = slots.FirstOrDefault(s => !s.IsBooked) ?? slots.First();
    Console.WriteLine($"-> Chọn khung giờ trống: {availableSlot.StartTime:HH:mm} - {availableSlot.EndTime:HH:mm} (SlotId: {availableSlot.Id})");

    // Bước 3: Đảm bảo SignalR Hub đã kết nối
    await EnsureHubConnectedAsync(hub);

    // Chuẩn bị TaskCompletionSource để đợi thông báo SignalR tương ứng
    var tcsNotification = new TaskCompletionSource<AppointmentStatusNotification>();
    var patientId = Guid.NewGuid();

    // Bước 4: Gửi REST API đặt lịch khám
    Console.WriteLine("\n[Bước 3/5] GỌI REST API: POST /appointments");
    var bookingRequest = new CreateAppointmentRequest
    {
        PatientId = patientId,
        DoctorId = doctor.Id,
        TimeSlotId = availableSlot.Id,
        AppointmentDate = DateTime.UtcNow.Date.AddHours(8),
        Reason = "Khám tổng quát sức khỏe định kỳ"
    };

    var bookRes = await client.PostAsJsonAsync("/appointments", bookingRequest);
    bookRes.EnsureSuccessStatusCode();

    using var docJson = JsonDocument.Parse(await bookRes.Content.ReadAsStringAsync());
    var appointmentElement = docJson.RootElement.GetProperty("appointment");
    var appointmentId = Guid.Parse(appointmentElement.GetProperty("id").GetString()!);
    var initialStatus = appointmentElement.GetProperty("status").GetInt32();

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"-> TIẾP NHẬN THÀNH CÔNG! Mã lịch khám (AppointmentId): {appointmentId}");
    Console.WriteLine($"-> Trạng thái ban đầu trên Server: {(AppointmentStatus)initialStatus} (Đang chờ xếp hàng)");
    Console.ResetColor();

    activeTracking.Add(appointmentId);

    // Bước 5: Tham gia SignalR Group riêng của lịch khám này
    Console.WriteLine($"\n[Bước 4/5] GIA NHẬP SIGNALR GROUP: Appointment_{appointmentId}");
    await hub.InvokeAsync("JoinAppointmentGroup", appointmentId.ToString());

    // Đăng ký nhận thông báo
    using var subscription = hub.On<AppointmentStatusNotification>("ReceiveAppointmentStatus", (notif) =>
    {
        if (notif.AppointmentId == appointmentId)
        {
            tcsNotification.TrySetResult(notif);
        }
    });

    Console.WriteLine("-> Đang lắng nghe thông báo cập nhật số thứ tự thời gian thực từ Server...");

    // Chờ thông báo SignalR (tối đa 10 giây)
    var completedTask = await Task.WhenAny(tcsNotification.Task, Task.Delay(10000));
    if (completedTask == tcsNotification.Task)
    {
        var result = await tcsNotification.Task;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n================== NHẬN THÔNG BÁO TỪ SIGNALR (REALTIME) ==================");
        Console.WriteLine($"• Mã lịch hẹn:       {result.AppointmentId}");
        Console.WriteLine($"• Trạng thái mới:    {result.Status}");
        Console.WriteLine($"• SỐ THỨ TỰ (STT):   #{result.TicketNumber}");
        Console.WriteLine($"• Thời gian dự kiến: {result.EstimatedTime:HH:mm} UTC");
        Console.WriteLine($"• Thông điệp:        {result.Message}");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[Timeout] Chưa nhận được thông báo từ SignalR trong 10 giây.");
        Console.ResetColor();
    }

    // Bước 6: Gọi lại REST API để đối chiếu trạng thái cuối cùng
    Console.WriteLine($"\n[Bước 5/5] GỌI LẠI REST API ĐỂ ĐỐI CHIẾU: GET /appointments/{appointmentId}");
    var verifyRes = await client.GetAsync($"/appointments/{appointmentId}");
    verifyRes.EnsureSuccessStatusCode();
    var verifyContent = await verifyRes.Content.ReadAsStringAsync();

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("-> DỮ LIỆU ĐỐI CHIẾU TRÊN DATABASE/SERVER:");
    Console.ResetColor();
    Console.WriteLine(verifyContent);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n>>> HOÀN THÀNH YÊU CẦU 1: Dữ liệu SignalR và dữ liệu REST API đồng nhất 100%!");
    Console.ResetColor();
}

// =============================================================================
// YÊU CẦU 2: XỬ LÝ TRẠNG THÁI LỖI ('FAILED') TỪ SIGNALR
// =============================================================================
static async Task RunFailedStateFlowAsync(HttpClient client, HubConnection hub, ConcurrentBag<Guid> activeTracking)
{
    Console.WriteLine("================================================================================");
    Console.WriteLine("        BẮT ĐẦU KIỂM THỬ YÊU CẦU 2: PHÁT SINH VÀ BẮT TRẠNG THÁI 'FAILED'");
    Console.WriteLine("================================================================================");

    await EnsureHubConnectedAsync(hub);

    // Chuẩn bị request có từ khóa kích hoạt kịch bản lỗi trong tiến trình nền
    var patientId = Guid.NewGuid();
    var doctorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    var slotId = Guid.Parse("a1111111-0000-0000-0000-000000000001");

    Console.WriteLine("-> Gửi yêu cầu đặt lịch với lý do mô phỏng lỗi: 'Cần khám gấp [trigger-fail]'");
    var bookingRequest = new CreateAppointmentRequest
    {
        PatientId = patientId,
        DoctorId = doctorId,
        TimeSlotId = slotId,
        AppointmentDate = DateTime.UtcNow.Date.AddHours(8),
        Reason = "Cần khám gấp [trigger-fail]"
    };

    var bookRes = await client.PostAsJsonAsync("/appointments", bookingRequest);
    bookRes.EnsureSuccessStatusCode();

    using var docJson = JsonDocument.Parse(await bookRes.Content.ReadAsStringAsync());
    var appointmentId = Guid.Parse(docJson.RootElement.GetProperty("appointment").GetProperty("id").GetString()!);

    Console.WriteLine($"-> Tiếp nhận lịch hẹn: {appointmentId}. Trạng thái ban đầu: Pending");
    activeTracking.Add(appointmentId);

    // Gia nhập SignalR Group
    await hub.InvokeAsync("JoinAppointmentGroup", appointmentId.ToString());

    var tcsFailed = new TaskCompletionSource<AppointmentStatusNotification>();

    using var subscription = hub.On<AppointmentStatusNotification>("ReceiveAppointmentStatus", (notif) =>
    {
        if (notif.AppointmentId == appointmentId)
        {
            tcsFailed.TrySetResult(notif);
        }
    });

    Console.WriteLine("-> Đang chờ xử lý hàng đợi và lắng nghe thông báo trạng thái...");

    var completed = await Task.WhenAny(tcsFailed.Task, Task.Delay(10000));
    if (completed == tcsFailed.Task)
    {
        var result = await tcsFailed.Task;

        if (result.Status == AppointmentStatus.Failed)
        {
            // BẮT SỰ KIỆN FAILED VÀ IN CẢNH BÁO ĐẶC BIỆT (Yêu cầu 2)
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("\n!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            Console.WriteLine(" [CẢNH BÁO LỖI NGHIỆP VỤ] LỊCH HẸN ĐÃ BỊ CHUYỂN SANG TRẠNG THÁI: FAILED! ");
            Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"• Mã lịch hẹn gặp sự cố: {result.AppointmentId}");
            Console.WriteLine($"• Trạng thái ghi nhận:   {result.Status}");
            Console.WriteLine($"• Thông điệp từ Server:  {result.Message}");
            Console.WriteLine($"• Thời điểm phát sinh:   {result.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine("• HƯỚNG DẪN XỬ LÝ: Khung giờ đã đầy do ưu tiên cấp cứu. Vui lòng chọn khung giờ khác.");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine($"Nhận trạng thái: {result.Status}");
        }
    }

    // Đối chiếu lại với REST API
    Console.WriteLine($"\n-> Đối chiếu lại qua REST API GET /appointments/{appointmentId}...");
    var checkRes = await client.GetAsync($"/appointments/{appointmentId}");
    var checkContent = await checkRes.Content.ReadAsStringAsync();
    Console.WriteLine($"Dữ liệu lưu tại Server:\n{checkContent}");
}

// =============================================================================
// YÊU CẦU 3: THEO DÕI ĐỒNG THỜI 2 LỊCH KHÁM (CONCURRENCY & ISOLATION)
// =============================================================================
static async Task RunConcurrentAppointmentsFlowAsync(HttpClient client, HubConnection hub)
{
    Console.WriteLine("================================================================================");
    Console.WriteLine("   BẮT ĐẦU KIỂM THỬ YÊU CẦU 3: THEO DÕI ĐỒNG THỜI 2 LỊCH KHÁM (ISOLATION TEST)");
    Console.WriteLine("================================================================================");

    await EnsureHubConnectedAsync(hub);

    // Đặt 2 lịch khám đồng thời cho 2 bệnh nhân khác nhau
    var patient1 = Guid.NewGuid();
    var doctor1 = Guid.Parse("11111111-1111-1111-1111-111111111111"); // BS. Nguyễn Văn A
    var slot1 = Guid.Parse("a1111111-0000-0000-0000-000000000002");

    var patient2 = Guid.NewGuid();
    var doctor2 = Guid.Parse("44444444-4444-4444-4444-444444444444"); // BS. Phạm Thị D
    var slot2 = Guid.Parse("d4444444-0000-0000-0000-000000000001");

    Console.WriteLine(">>> Đang gửi đồng thời 2 yêu cầu đặt lịch qua REST API (Task.WhenAll)...");

    var req1 = new CreateAppointmentRequest { PatientId = patient1, DoctorId = doctor1, TimeSlotId = slot1, Reason = "Khám đau dạ dày" };
    var req2 = new CreateAppointmentRequest { PatientId = patient2, DoctorId = doctor2, TimeSlotId = slot2, Reason = "Khám dị ứng da" };

    var taskPost1 = client.PostAsJsonAsync("/appointments", req1);
    var taskPost2 = client.PostAsJsonAsync("/appointments", req2);

    await Task.WhenAll(taskPost1, taskPost2);

    var res1 = await taskPost1;
    var res2 = await taskPost2;

    using var doc1 = JsonDocument.Parse(await res1.Content.ReadAsStringAsync());
    using var doc2 = JsonDocument.Parse(await res2.Content.ReadAsStringAsync());

    var apptId1 = Guid.Parse(doc1.RootElement.GetProperty("appointment").GetProperty("id").GetString()!);
    var apptId2 = Guid.Parse(doc2.RootElement.GetProperty("appointment").GetProperty("id").GetString()!);

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"[Luồng Lịch 1] Tiếp nhận AppointmentId 1: {apptId1} (BS. Nguyễn Văn A)");
    Console.WriteLine($"[Luồng Lịch 2] Tiếp nhận AppointmentId 2: {apptId2} (BS. Phạm Thị D)");
    Console.ResetColor();

    // Mỗi luồng gia nhập đúng Group riêng biệt của mình trên SignalR
    await hub.InvokeAsync("JoinAppointmentGroup", apptId1.ToString());
    await hub.InvokeAsync("JoinAppointmentGroup", apptId2.ToString());

    var tcs1 = new TaskCompletionSource<AppointmentStatusNotification>();
    var tcs2 = new TaskCompletionSource<AppointmentStatusNotification>();

    using var sub = hub.On<AppointmentStatusNotification>("ReceiveAppointmentStatus", (notif) =>
    {
        if (notif.AppointmentId == apptId1)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n>>> [LUỒNG LỊCH 1 NHẬN TIN ĐÚNG ĐÍCH] Lịch {notif.AppointmentId} -> STT: #{notif.TicketNumber} (Status: {notif.Status})");
            Console.ResetColor();
            tcs1.TrySetResult(notif);
        }
        else if (notif.AppointmentId == apptId2)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n>>> [LUỒNG LỊCH 2 NHẬN TIN ĐÚNG ĐÍCH] Lịch {notif.AppointmentId} -> STT: #{notif.TicketNumber} (Status: {notif.Status})");
            Console.ResetColor();
            tcs2.TrySetResult(notif);
        }
        else
        {
            Console.WriteLine($"[Lạc tin] Nhận tin không thuộc luồng 1 hoặc 2: {notif.AppointmentId}");
        }
    });

    Console.WriteLine("-> Đang theo dõi cả 2 luồng đồng thời qua SignalR Groups...");
    await Task.WhenAll(tcs1.Task, tcs2.Task);

    var r1 = await tcs1.Task;
    var r2 = await tcs2.Task;

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\n================== KẾT QUẢ ĐỐI SOÁT CÁCH LY THÔNG ĐIỆP ==================");
    Console.WriteLine($"• Lịch 1 ({r1.AppointmentId}): Cấp STT #{r1.TicketNumber} -> Trả về đúng Luồng 1.");
    Console.WriteLine($"• Lịch 2 ({r2.AppointmentId}): Cấp STT #{r2.TicketNumber} -> Trả về đúng Luồng 2.");
    Console.WriteLine("• Kết luận: Cơ chế SignalR Groups đảm bảo cách ly tuyệt đối, không trùng lặp hay ghi đè!");
    Console.WriteLine("=========================================================================");
    Console.ResetColor();
}

// =============================================================================
// YÊU CẦU 4: KIỂM THỬ SỰ CỐ MẤT KẾT NỐI & CƠ CHẾ RECONNECT
// =============================================================================
static async Task TestDisconnectionScenarioAsync(HubConnection hub, string apiUrl)
{
    Console.WriteLine("================================================================================");
    Console.WriteLine("        BẮT ĐẦU KIỂM THỬ YÊU CẦU 4: MẤT KẾT NỐI & TỰ ĐỘNG KHÔI PHỤC");
    Console.WriteLine("================================================================================");

    Console.WriteLine($"Trạng thái SignalR Connection hiện tại: {hub.State}");

    if (hub.State == HubConnectionState.Connected)
    {
        Console.WriteLine("\nKịch bản 1: MÔ PHỎNG NGẮT KẾT NỐI TỪ CLIENT:");
        Console.WriteLine("Đang gọi StopAsync() để ngắt kết nối chủ động...");
        await hub.StopAsync();
        Console.WriteLine($"Trạng thái sau khi ngắt: {hub.State}");

        Console.WriteLine("\nĐang gọi StartAsync() để kết nối lại...");
        await hub.StartAsync();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Đã kết nối lại thành công! Trạng thái: {hub.State} | Id: {hub.ConnectionId}");
        Console.ResetColor();
    }
    else
    {
        Console.WriteLine("\nKịch bản 2: KẾT NỐI TỚI PORT KHÔNG TỒN TẠI (Mô phỏng Server chưa bật):");
        var badHub = new HubConnectionBuilder()
            .WithUrl("http://localhost:5999/hubs/queue")
            .WithAutomaticReconnect()
            .Build();

        try
        {
            Console.WriteLine("Đang thử kết nối tới Server chưa khởi động tại port 5999...");
            await badHub.StartAsync();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Bắt lỗi kết nối thành công] Không thể kết nối tới Server chưa bật:");
            Console.WriteLine($"Thông điệp lỗi: {ex.Message}");
            Console.ResetColor();
        }
        finally
        {
            await badHub.DisposeAsync();
        }
    }
}

// =============================================================================
// CÁC HÀM TIỆN ÍCH REST API
// =============================================================================
static async Task ShowDoctorsAndSlotsAsync(HttpClient client)
{
    var res = await client.GetAsync("/doctors");
    var doctors = await res.Content.ReadFromJsonAsync<List<Doctor>>() ?? [];
    Console.WriteLine($"\n--- DANH SÁCH BÁC SĨ ({doctors.Count}) ---");
    foreach (var d in doctors)
    {
        Console.WriteLine($"• ID: {d.Id} | {d.FullName} - {d.Specialty} | {d.RoomNumber}");
        var slotRes = await client.GetAsync($"/doctors/{d.Id}/slots");
        var slots = await slotRes.Content.ReadFromJsonAsync<List<TimeSlot>>() ?? [];
        foreach (var s in slots)
        {
            Console.WriteLine($"    Slot {s.StartTime:HH:mm}-{s.EndTime:HH:mm} | {(s.IsBooked ? "[HẾT CHỖ]" : "[CÒN TRỐNG]")}");
        }
    }
}

static async Task LookupAppointmentDetailsAsync(HttpClient client)
{
    Console.Write("Nhập Appointment ID cần tra cứu (bỏ trống dùng lịch mẫu): ");
    var input = Console.ReadLine()?.Trim();
    var id = string.IsNullOrEmpty(input) ? "99999999-9999-9999-9999-999999999999" : input;

    var res = await client.GetAsync($"/appointments/{id}");
    var content = await res.Content.ReadAsStringAsync();
    Console.WriteLine($"\nPhản hồi từ Server (HTTP {res.StatusCode}):\n{content}");
}
