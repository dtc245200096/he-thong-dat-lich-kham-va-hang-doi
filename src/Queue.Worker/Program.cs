using Queue.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Cấu hình logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Đăng ký BackgroundService xử lý hàng đợi
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var configuration = host.Services.GetRequiredService<IConfiguration>();

var rabbitMqHost = configuration["RabbitMQ:HostName"] ?? "localhost";
var rabbitMqPort = configuration.GetValue<int>("RabbitMQ:Port", 5672);
var connectionString = configuration.GetConnectionString("DefaultConnection");

logger.LogInformation(">>> [Queue.Worker] Initialized successfully.");
logger.LogInformation(">>> Database Connection: {ConnectionString}", 
    string.IsNullOrEmpty(connectionString) ? "Not Configured" : "Configured from Environment/Config");
logger.LogInformation(">>> RabbitMQ Broker: {Host}:{Port}", rabbitMqHost, rabbitMqPort);

await host.RunAsync();
