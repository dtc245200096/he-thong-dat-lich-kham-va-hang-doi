# KIẾN TRÚC HỆ THỐNG - ĐỀ TÀI 09: HỆ THỐNG ĐẶT LỊCH KHÁM VÀ QUẢN LÝ HÀNG ĐỢI

## 1. TỔNG QUAN HỆ THỐNG (SYSTEM OVERVIEW)
Hệ thống giải quyết bài toán quá tải tại các cơ sở y tế bằng mô hình kiến trúc phân tán hướng sự kiện (Event-Driven Architecture), đảm bảo:
- **Tách biệt mối quan tâm (SoC):** Tách luồng tiếp nhận lịch hẹn (Web API) khỏi luồng xử lý tính toán số thứ tự (Worker Service) và luồng thông báo thời gian thực (Notification Service).
- **Khả năng chịu tải và độ tin cậy:** Message Broker (RabbitMQ) hoạt động như một bộ đệm chống nghẽn (backpressure buffer), đảm bảo các request đặt lịch không bị thất thoát ngay cả khi lượt truy cập tăng đột biến.
- **Trải nghiệm thời gian thực:** Client nhận được cập nhật số thứ tự, thời gian dự kiến và thông báo gọi số qua WebSocket/SignalR mà không cần liên tục gửi request thăm dò (polling).

---

## 2. SƠ ĐỒ LUỒNG THÔNG TIN VÀ KIẾN TRÚC PHÂN TÁN

```
+-----------------------------------------------------------------------------------+
|                                PATIENT CLIENT                                     |
|           (Console UI / Web SPA - Tra cứu, Đặt lịch, Xem hàng đợi)                |
+----------------------+------------------------------------+-----------------------+
                       |                                    ^
       1. HTTP Request |                                    | 5. Real-time Push
       (REST API)      |                                    | (WebSocket / SignalR)
                       v                                    |
+-------------------------------------+             +-------+-----------------------+
|         Appointment.Api             |             |     Notification.Service      |
|   (ASP.NET Core Minimal API)        |             |  (Minimal API + SignalR Hub)  |
+------------------+------------------+             +---------------+---------------+
                   |                                                ^
                   | 2. Publish                                     | 4. Consume
                   | AppointmentCreatedEvent                        | QueueTicketGeneratedEvent
                   v                                                | & QueueUpdatedEvent
+-------------------------------------------------------------------+---------------+
|                            MESSAGE BROKER: RabbitMQ                               |
|   Exchanges: clinic.appointment.exchange, clinic.notification.exchange            |
|   Queues:    clinic.appointment.created.queue, clinic.queueticket.generated.queue |
+-------------------------------------------------------------------+---------------+
                                                                    ^
                                                                    | 3. Publish
                                                                    | Events
                                                                    |
+-------------------------------------------------------------------+---------------+
|                              Queue.Worker                                         |
|    (.NET Background Worker - Tính toán STT, Ước lượng giờ khám, Cập nhật Queue)  |
+-------------------------------------+---------------------------------------------+
                                      |
                                      | Đọc / Ghi trạng thái
                                      v
+-----------------------------------------------------------------------------------+
|                        DATABASE (PostgreSQL / SQL Server)                         |
|   Tables: Patients, Doctors, TimeSlots, Appointments, QueueTickets                |
+-----------------------------------------------------------------------------------+
```

---

## 3. CÁC THÀNH PHẦN CHÍNH (COMPONENTS)

### 3.1. `Shared.Contracts` (Class Library)
- **Vai trò:** Thư viện hợp đồng dữ liệu chung, được tham chiếu bởi tất cả các service để đảm bảo tính nhất quán của schema.
- **Thành phần:**
  - **Models / Entities:** `Patient`, `Doctor`, `TimeSlot`, `Appointment`, `QueueTicket`.
  - **Enums:** `AppointmentStatus` (`Pending`, `Confirmed`, `InProgress`, `Completed`, `Cancelled`).
  - **DTOs:** `CreateAppointmentRequest`, `QueueStatusResponse`.
  - **Events:**
    - `AppointmentCreatedEvent`: Phát sinh khi lịch hẹn được tiếp nhận.
    - `QueueTicketGeneratedEvent`: Phát sinh khi worker đã cấp số thứ tự.
    - `QueueUpdatedEvent`: Phát sinh khi trạng thái hàng đợi thay đổi (bác sĩ gọi số, hoàn tất ca khám).

### 3.2. `Appointment.Api` (ASP.NET Core Minimal API)
- **Vai trò:** Gateway tiếp nhận request từ phía Client, cung cấp 6 REST API cơ bản.
- **Hành vi luồng nghiệp vụ:**
  - Khi nhận `POST /appointments`, API lưu trạng thái tạm thời vào Database và gửi ngay `AppointmentCreatedEvent` lên RabbitMQ (tránh block client).
  - Trả về mã HTTP `201 Created` ngay lập tức kèm mã lịch hẹn.
- **Cấu hình:** Đọc từ `appsettings.json` hoặc Environment Variables: `ConnectionStrings__DefaultConnection`, `RabbitMQ__HostName`, `RabbitMQ__Port`.

### 3.3. `Queue.Worker` (.NET Background Service)
- **Vai trò:** Dịch vụ hậu trường xử lý hàng đợi và cấp số thứ tự.
- **Hành vi luồng nghiệp vụ:**
  - Lắng nghe hàng đợi `clinic.appointment.created.queue`.
  - Đảm bảo tính toán số thứ tự tuần tự trong ngày cho từng bác sĩ (tránh Race Condition).
  - Ước tính thời gian khám dựa trên số lượng bệnh nhân đang chờ và thời lượng trung bình mỗi ca khám.
  - Cập nhật bản ghi `QueueTicket` vào Database.
  - Phát sự kiện `QueueTicketGeneratedEvent` và `QueueUpdatedEvent` vào `clinic.notification.exchange`.

### 3.4. `Notification.Service` (Minimal API + SignalR)
- **Vai trò:** Quản lý kết nối WebSocket và đẩy thông báo thời gian thực xuống Client.
- **Thành phần:**
  - `QueueHub : Hub`: Quản lý client kết nối vào các nhóm theo dõi:
    - Nhóm theo bác sĩ: `DoctorQueue_{doctorId}` (dùng cho bảng điện tử hiển thị tại phòng khám).
    - Nhóm theo bệnh nhân: `Patient_{patientId}` (nhận thông báo cá nhân khi sắp tới lượt).
  - Consumer lắng nghe RabbitMQ để tự động trigger `IHubContext<QueueHub>` gửi broadcast xuống Client.

### 3.5. `Patient.Client` (Console Application)
- **Vai trò:** Mô phỏng ứng dụng người dùng, cho phép:
  - Xem danh mục bác sĩ và ca khám còn trống.
  - Gửi yêu cầu đặt lịch khám bệnh.
  - Tra cứu thông tin lịch hẹn và hàng đợi.
  - Sẵn sàng kết nối SignalR Hub ở Phase 4 để nhận thông báo gọi số.

---

## 4. CHIẾN LƯỢC QUẢN LÝ CẤU HÌNH (CONFIGURATION STRATEGY)
- **Quy tắc tuyệt đối:** Không hardcode IP, Port, Connection String hoặc credentials trong code C#.
- **Thứ tự ưu tiên nạp cấu hình (.NET Configuration Hierarchy):**
  1. Environment Variables (biến môi trường từ Docker/OS).
  2. `appsettings.{Environment}.json` (ví dụ `appsettings.Development.json`).
  3. `appsettings.json` (cấu hình mặc định).
- **Ánh xạ biến môi trường trong Docker Compose:**
  - `ConnectionStrings__DefaultConnection`: Chuỗi kết nối tới cơ sở dữ liệu container.
  - `RabbitMQ__HostName`: Tên service container `rabbitmq` trong mạng Docker.
  - `RabbitMQ__Port`: Port nội bộ `5672`.

---

## 5. LỘ TRÌNH TRIỂN KHAI THEO GIÁO TRÌNH (MILESTONES)
- **Phase 1 (Hiện tại):** Khởi tạo khung dự án (Scaffolding), định nghĩa Contracts, thiết lập REST API Minimal API, Worker Service khung, SignalR Hub khung và cấu hình Docker Compose.
- **Phase 2 (Database):** Cấu hình Entity Framework Core / Dapper, Migration schema bảng dữ liệu trên PostgreSQL / SQL Server.
- **Phase 3 (Message Broker):** Tích hợp RabbitMQ Client (Producer trong Appointment.Api, Consumer trong Queue.Worker), xử lý Dead-Letter Queue (DLQ) và Message Acknowledgement.
- **Phase 4 (Real-time & Resilience):** Hoàn thiện SignalR Hub, áp dụng Polly (Retry, Circuit Breaker) và Health Checks hoàn chỉnh.

