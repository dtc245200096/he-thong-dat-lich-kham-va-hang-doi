using Microsoft.AspNetCore.SignalR;
using Notification.Service.Hubs;

var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// CẤU HÌNH HỆ THỐNG & ĐỌC BIẾN MÔI TRƯỜNG / APPSETTINGS
// =============================================================================
var rabbitMqHost = builder.Configuration["RabbitMQ:HostName"] ?? "localhost";
var rabbitMqPort = builder.Configuration.GetValue<int>("RabbitMQ:Port", 5672);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Cấu hình SignalR
builder.Services.AddSignalR();

// Cấu hình CORS cho SignalR client kết nối từ web/mobile
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
logger.LogInformation(">>> [Notification.Service] Starting service...");
logger.LogInformation(">>> RabbitMQ Broker: {Host}:{Port}", rabbitMqHost, rabbitMqPort);

app.UseCors();

// =============================================================================
// ROOT & HEALTH CHECK
// =============================================================================
app.MapGet("/", () => Results.Ok(new
{
    Service = "Notification.Service",
    Status = "Healthy",
    SignalREndpoint = "/hubs/queue",
    Version = "1.0.0",
    Timestamp = DateTime.UtcNow
}));

// =============================================================================
// ĐĂNG KÝ SIGNALR HUB
// =============================================================================
app.MapHub<QueueHub>("/hubs/queue");

// =============================================================================
// TODO: Phase 4 - RabbitMQ Consumer Hosted Service inside Notification.Service
// Ở buổi học SignalR + RabbitMQ, chúng ta sẽ thêm:
// 1. Một BackgroundService tiêu thụ sự kiện QueueTicketGeneratedEvent & QueueUpdatedEvent từ RabbitMQ.
// 2. Inject IHubContext<QueueHub> vào consumer để broadcast message realtime xuống các Client:
//    await hubContext.Clients.Group($"DoctorQueue_{doctorId}").SendAsync("ReceiveQueueUpdate", eventData);
// =============================================================================

// Mock endpoint trigger test thông báo (dùng để kiểm thử SignalR trong tương lai)
app.MapPost("/test-broadcast/{doctorId:guid}", async (Guid doctorId, IHubContext<QueueHub> hubContext) =>
{
    logger.LogInformation("[Test Broadcast] Sending test queue update to doctor group: {DoctorId}", doctorId);
    
    await hubContext.Clients.Group($"DoctorQueue_{doctorId}").SendAsync("ReceiveQueueUpdate", new
    {
        DoctorId = doctorId,
        Message = "Đây là thông báo test từ Notification.Service",
        Timestamp = DateTime.UtcNow
    });

    return Results.Ok(new { Message = "Đã gửi broadcast test thành công." });
});

app.Run();
