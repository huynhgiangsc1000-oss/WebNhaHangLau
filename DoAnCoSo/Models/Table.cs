using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DoAnCoSo.Models
{
    public class Table
    {
        // Định nghĩa các hằng số trạng thái để dùng thống nhất trong toàn hệ thống
        public const string StatusEmpty = "Empty";       // Bàn trống hoàn toàn
        public const string StatusReserved = "Reserved"; // Đã có người đặt trước
        public const string StatusOccupied = "Occupied"; // Khách đang ngồi tại bàn

        [Key]
        public int TableId { get; set; }

        [Required(ErrorMessage = "Tên bàn không được để trống")]
        public string TableName { get; set; }

        [Range(1, 50, ErrorMessage = "Sức chứa phải từ 1 đến 50 người")]
        public int Capacity { get; set; }
        // Giá trị mặc định khi tạo bàn mới là "Empty"
        public string Status { get; set; } = StatusEmpty;

        public string? QrCode { get; set; }

        // Quan hệ: Một bàn có thể có nhiều đơn hàng (theo lịch sử)
        public virtual ICollection<Order>? Orders { get; set; }

        // Quan hệ: Một bàn có thể nằm trong nhiều lịch đặt trước
        public virtual ICollection<Booking>? Bookings { get; set; }
    }
}