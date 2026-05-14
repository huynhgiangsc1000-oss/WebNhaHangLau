using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoAnCoSo.Models
{
    public class Booking
    {
        [Key]
        public int BookingId { get; set; }

        [Required(ErrorMessage = "Tên khách hàng không được để trống")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn thời gian")]
        public DateTime BookingDate { get; set; } // Ngày giờ khách hẹn đến

        [Range(1, 50, ErrorMessage = "Số lượng khách từ 1-50 người")]
        public int GuestCount { get; set; }

        public string? Note { get; set; }

        // Trạng thái đơn đặt bàn: Pending (Chờ), Confirmed (Đã xác nhận), Cancelled (Hủy)
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? CheckInCode { get; set; }

        public string? Email { get; set; }

        // Liên kết với Table (Sau khi Admin chọn bàn cho khách)[cite: 7]
        public int? TableId { get; set; }
        [ForeignKey("TableId")]
        public virtual Table? Table { get; set; }

        // Liên kết với User (nếu khách đã đăng nhập)[cite: 8]
        public int? UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}