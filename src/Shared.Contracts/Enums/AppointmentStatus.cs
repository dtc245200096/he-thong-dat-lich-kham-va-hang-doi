namespace Shared.Contracts.Enums;

/// <summary>
/// Trạng thái của một lịch hẹn khám bệnh.
/// </summary>
public enum AppointmentStatus
{
    /// <summary>
    /// Đang chờ xác nhận / xếp vào hàng đợi.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Đã được cấp số thứ tự và xác nhận vào hàng đợi.
    /// </summary>
    Confirmed = 1,

    /// <summary>
    /// Đang trong quá trình khám bệnh.
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// Đã hoàn tất buổi khám.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Đã bị hủy bỏ bởi bệnh nhân hoặc phòng khám.
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// Xử lý thất bại (khung giờ đầy đột xuất, sự cố bác sĩ hoặc lỗi xếp hàng).
    /// </summary>
    Failed = 5
}
