using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoAnCoSo.Models
{
    public class Order
    {
        public const string StatusPending = "Pending";     // Mới đặt, chưa trả tiền
        public const string StatusCompleted = "Completed"; // Đã ăn xong & thanh toán thành công
        public const string StatusCancelled = "Cancelled"; // Đơn bị hủy
        public const string StatusPreOrder = "PreOrder";   // Đã xác nhận đặt bàn, chờ đến giờ
        [Key]
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.Now;
        // Trong Models/Order.cs
        public string? PaymentMethod { get; set; } // Lưu "Tiền mặt" hoặc "Chuyển khoản"
        public int TableId { get; set; }
        [ForeignKey("TableId")]
        public virtual Table? Table { get; set; }
        public int? UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Pending";
        public int? PromotionId { get; set; }
        [ForeignKey("PromotionId")]
        public virtual Promotion? Promotion { get; set; }

        // Lưu số tiền được giảm để dễ dàng in hóa đơn
        public decimal DiscountAmount { get; set; }

        // Phần trăm giảm giá thực tế đã áp dụng (Mức cao nhất giữa Rank và Voucher)
        public decimal AppliedDiscountPercent { get; set; }
        public int? BookingId { get; set; }

        [ForeignKey("BookingId")]
        public virtual Booking? Booking { get; set; }
        [NotMapped]
        public decimal OriginalAmount => TotalAmount + DiscountAmount;
        public virtual ICollection<OrderDetail> OrderDetails { get; set; }
    }
}
