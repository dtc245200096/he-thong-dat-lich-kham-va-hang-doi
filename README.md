# ĐỀ TÀI 09: HỆ THỐNG ĐẶT LỊCH KHÁM VÀ QUẢN LÝ HÀNG ĐỢI
> **Học phần:** Phát triển ứng dụng phân tán (Distributed Application Development)  
> **Nền tảng:** C#, .NET 10, ASP.NET Core Minimal API, RabbitMQ, SignalR, Docker

---

## 1. MÔ TẢ BÀI TOÁN (PROBLEM STATEMENT)
Tại các bệnh viện và phòng khám hiện đại, tình trạng bệnh nhân phải xếp hàng chờ đợi trực tiếp tại chỗ gây ra sự quá tải, ùn tắc và lãng phí thời gian. **Hệ thống đặt lịch khám và quản lý hàng đợi** được xây dựng nhằm mục tiêu:
1. Cho phép bệnh nhân chủ động đặt lịch khám theo bác sĩ, chuyên khoa và khung giờ mong muốn.
2. Tự động xếp hàng đợi, cấp số thứ tự (Ticket Number) và ước lượng thời gian khám chính xác.
3. Thông báo trạng thái hàng đợi và thông báo gọi số thời gian thực (Real-time) tới bệnh nhân qua SignalR/WebSocket.
4. Áp dụng kiến trúc phân tán (Distributed Architecture) với Message Broker (RabbitMQ) để giảm tải cho hệ thống, đảm bảo tính sẵn sàng cao và không nghẽn dịch vụ vào giờ cao điểm.

---

## 2. SƠ ĐỒ KIẾN TRÚC HỆ THỐNG (ASCII ARCHITECTURE DIAGRAM)

```
===================================================================================================
                             SƠ ĐỒ LUỒNG KIẾN TRÚC PHÂN TÁN
===================================================================================================

[Patient Client] -------- (1) HTTP Request: Đặt lịch khám --------> [Appointment.Api]
       ^                                                                   |
       |                                                            (2) Ghi nhận & Lưu
       |                                                                   v
       |                                                              [ Database ]
       |                                                                   ^
       |                                                                   |
       |                   (3) Event: AppointmentCreated                   | (4) Đọc/Ghi STT
       |                   ----------------------------->                  |
       |                   |                            |                  v
[Appointment.Api] ----> [RabbitMQ] ------------------------> [Queue.Worker]
                             |
                             | (5) Event: QueueTicketGenerated & QueueUpdated
                             v
                  [Notification.Service]
                             |
                             | (6) Real-time Push (SignalR / WebSocket)
                             v
                      [Patient Client]
===================================================================================================
```

### Các luồng tương tác chính:
- **Luồng 1 - Tiếp nhận:** `Patient Client -> Appointment API -> Database`
- **Luồng 2 - Xếp số thứ tự:** `Appointment API -> RabbitMQ -> Queue Worker -> Database`
- **Luồng 3 - Thông báo thời gian thực:** `Queue Worker -> RabbitMQ Event -> Notification Service -> SignalR -> Patient Client`

---

## 3. DANH SÁCH 8 CHỨC NĂNG CHÍNH CỦA ĐỀ TÀI 09
1. **Quản lý danh mục Bác sĩ & Chuyên khoa:** Tra cứu danh sách bác sĩ đang làm việc, phòng khám tương ứng và chuyên khoa y tế.
2. **Quản lý Khung giờ khám (TimeSlots):** Hiển thị các khung giờ làm việc còn trống của bác sĩ theo ngày.
3. **Tiếp nhận đặt lịch khám bệnh (Booking):** Bệnh nhân đăng ký thông tin, chọn bác sĩ, khung giờ và lý do khám.
4. **Cấp số thứ tự tự động (Queue Number Generation):** Worker Service tự động tính toán số thứ tự tuần tự trong ngày, tránh trùng lặp số khi nhiều bệnh nhân đặt cùng lúc.
5. **Ước lượng thời gian khám (Estimated Wait Time):** Tính toán thời gian dự kiến tới lượt khám dựa trên số lượng bệnh nhân đang chờ.
6. **Theo dõi trạng thái hàng đợi trực tuyến (Live Queue Tracking):** Bệnh nhân và màn hình hiển thị tại phòng khám có thể theo dõi số đang khám, số tiếp theo và số người còn lại.
7. **Thông báo gọi số & Cập nhật thời gian thực (Real-time Calling):** Đẩy thông báo tức thì qua SignalR khi bác sĩ hoàn thành ca khám và gọi số tiếp theo.
8. **Hủy lịch khám & Đồng bộ hàng đợi (Cancellation):** Cho phép hủy lịch khám và tự động giải phóng khung giờ, điều chỉnh hàng đợi.

---

## 4. DANH SÁCH 6 REST API TỐI THIỂU (`Appointment.Api`)
Tất cả 6 API đã được cấu hình khung Minimal API tại `src/Appointment.Api/Program.cs`:

| STT | Phương thức | Đường dẫn API | Mô tả nghiệp vụ | Trạng thái hiện tại |
|:---:|:-----------:|:--------------|:----------------|:---------------------|
| 1 | `GET` | `/doctors` | Lấy danh sách bác sĩ đang hoạt động | Mock dữ liệu khung (Sẵn sàng cho Phase 2) |
| 2 | `GET` | `/doctors/{id}/slots` | Lấy danh sách khung giờ còn trống của bác sĩ | Mock dữ liệu khung |
| 3 | `POST` | `/appointments` | Tiếp nhận yêu cầu đặt lịch khám mới | Tiếp nhận DTO, sinh Model, sẵn sàng kết nối RabbitMQ |
| 4 | `DELETE` | `/appointments/{id}` | Hủy bỏ một lịch hẹn khám đã đặt | Mock xử lý hủy, sẵn sàng đồng bộ hàng đợi |
| 5 | `GET` | `/appointments/{id}` | Tra cứu chi tiết thông tin lịch hẹn | Trả về chi tiết lịch hẹn theo Id |
| 6 | `GET` | `/queues/{doctorId}` | Xem trạng thái hàng đợi hiện tại của phòng khám | Trả về số đang khám, số đang chờ và danh sách vé |

---

## 5. CẤU TRÚC THƯ MỤC DỰ ÁN (PROJECT STRUCTURE)

```
.
├── ClinicQueueSystem.sln            # Solution chính của hệ thống (.NET 10)
├── docker-compose.yml               # Cấu hình khởi chạy toàn bộ hệ thống bằng Docker
├── .gitignore                       # File loại trừ git chuẩn cho .NET & Docker
├── nuget.config                     # Cấu hình nguồn nạp package NuGet
├── build.ps1                        # Script hỗ trợ build nhanh toàn bộ các project
├── README.md                        # Tài liệu tổng quan & hướng dẫn
├── docs/
│   └── ARCHITECTURE.md              # Tài liệu chi tiết kiến trúc phân tán & hợp đồng dữ liệu
└── src/
    ├── Shared.Contracts/            # Class Library chứa DTOs, Models & Events dùng chung
    │   ├── Enums/                   # AppointmentStatus
    │   ├── Models/                  # Patient, Doctor, TimeSlot, Appointment, QueueTicket
    │   ├── DTOs/                    # CreateAppointmentRequest, QueueStatusResponse
    │   └── Events/                  # AppointmentCreatedEvent, QueueTicketGeneratedEvent, QueueUpdatedEvent
    ├── Appointment.Api/             # ASP.NET Core Minimal API tiếp nhận lịch khám
    │   ├── Program.cs               # Khởi tạo 6 REST API khung & đọc cấu hình
    │   ├── appsettings.json         # Cấu hình kết nối DB, RabbitMQ, Service URLs
    │   └── Dockerfile               # Cấu hình container hóa service
    ├── Queue.Worker/                # .NET Worker Service xử lý hàng đợi hậu trường
    │   ├── Worker.cs                # BackgroundService khung (chờ tích hợp RabbitMQ Consumer)
    │   ├── Program.cs               # Entrypoint & Logging
    │   ├── appsettings.json         # Cấu hình RabbitMQ & Database
    │   └── Dockerfile               # Cấu hình container hóa service
    ├── Notification.Service/        # ASP.NET Core Minimal API + SignalR
    │   ├── Hubs/QueueHub.cs         # SignalR Hub quản lý nhóm bác sĩ & phòng chờ
    │   ├── Program.cs               # Cấu hình CORS, SignalR & Broadcast endpoint
    │   ├── appsettings.json         # Cấu hình SignalR, CORS, RabbitMQ
    │   └── Dockerfile               # Cấu hình container hóa service
    └── Patient.Client/              # Console Client mô phỏng giao diện người dùng
        ├── Program.cs               # Menu tương tác gửi HTTP Request tới API
        ├── appsettings.json         # Cấu hình URL các service
        └── Dockerfile               # Cấu hình container hóa client
```

---

## 6. HƯỚNG DẪN BUILD VÀ CHẠY DỰ ÁN

### 6.1. Yêu cầu môi trường
- [.NET SDK 10.0](https://dotnet.microsoft.com/) trở lên.
- [Docker](https://www.docker.com/) & Docker Compose (tùy chọn khi chạy container).

### 6.2. Build mã nguồn trực tiếp
Bạn có thể build từng project hoặc chạy script hỗ trợ:
```bash
# Build từng project
dotnet build src/Shared.Contracts/Shared.Contracts.csproj
dotnet build src/Appointment.Api/Appointment.Api.csproj
dotnet build src/Queue.Worker/Queue.Worker.csproj
dotnet build src/Notification.Service/Notification.Service.csproj
dotnet build src/Patient.Client/Patient.Client.csproj

# Hoặc chạy script PowerShell tại thư mục gốc:
.\build.ps1
```

### 6.3. Khởi chạy toàn bộ hệ thống bằng Docker Compose
Để khởi chạy toàn bộ hạ tầng gồm RabbitMQ, PostgreSQL Database, Appointment API, Queue Worker và Notification Service:
```bash
docker compose up -d --build
```
- **RabbitMQ Management Dashboard:** `http://localhost:15672` (Username: `guest` / Password: `guest`)
- **Appointment API Health Check:** `http://localhost:5001/`
- **Notification Service SignalR Hub:** `http://localhost:5003/hubs/queue`

---

## 7. LỘ TRÌNH PHÁT TRIỂN QUA CÁC BUỔI HỌC (LEARNING ROADMAP)

- [x] **Phase 1: Khởi tạo Khung dự án (Scaffolding) & REST API**
  - Thiết lập Solution .NET 10 và 5 Projects độc lập.
  - Xây dựng Models, Enums, DTOs và Events dùng chung trong `Shared.Contracts`.
  - Khởi tạo 6 endpoint REST Minimal API với logging và đọc cấu hình từ môi trường.
  - Tạo khung `BackgroundService` cho `Queue.Worker` và `QueueHub` cho `Notification.Service`.
  - Viết `Dockerfile` cho từng service và `docker-compose.yml` tích hợp sẵn RabbitMQ & PostgreSQL.

- [ ] **Phase 2: Tích hợp Cơ sở dữ liệu (Database & Persistence)**
  - Cấu hình Entity Framework Core / Dapper với PostgreSQL.
  - Viết migrations tạo các bảng `Patients`, `Doctors`, `TimeSlots`, `Appointments`, `QueueTickets`.
  - Thay thế Mock data trong `Appointment.Api` bằng truy vấn thực tế.

- [ ] **Phase 3: Tích hợp Message Broker (RabbitMQ)**
  - Tích hợp `RabbitMQ.Client` trong `Appointment.Api` (Producer gửi `AppointmentCreatedEvent`).
  - Hoàn thiện Consumer trong `Queue.Worker` để lắng nghe, cấp số thứ tự và cập nhật Database.
  - Producer trong `Queue.Worker` gửi `QueueTicketGeneratedEvent` và `QueueUpdatedEvent`.

- [ ] **Phase 4: Real-time SignalR, Resilience & Hoàn thiện**
  - Hoàn thiện `Notification.Service` tiêu thụ sự kiện từ RabbitMQ và đẩy thông báo qua `QueueHub`.
  - Bổ sung Polly xử lý Retry Policy, Circuit Breaker khi kết nối mạng/broker gặp sự cố.
  - Tích hợp kiểm tra sức khỏe hệ thống (Health Checks UI).

