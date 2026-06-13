using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoAnCoSo.Models
{
    public class Booking
    {
        [Key]
        public int BookingId { get; set; }

        [Required(ErrorMessage = "Tên khách hàng không được để trống")]
        [StringLength(100, ErrorMessage = "Tên không được vượt quá 100 ký tự")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn thời gian")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Thời gian đặt")]
        public DateTime BookingDate { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Vui lòng nhập số lượng khách")]
        [Range(1, 50, ErrorMessage = "Số lượng khách từ 1-50 người")]
        [Display(Name = "Số lượng khách")]
        public int GuestCount { get; set; }

        [Display(Name = "Mã giảm giá")]
        [StringLength(20, ErrorMessage = "Mã giảm giá không hợp lệ")]
        public string? DiscountCode { get; set; }

        [Display(Name = "Ghi chú")]
        public string? Note { get; set; }

        [Display(Name = "Trạng thái")]
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Mã xác nhận")]
        public string? CheckInCode { get; set; }

        // --- BỔ SUNG: Lưu tổng tiền vào Database ---
        [Display(Name = "Tổng tiền")]
        [Column(TypeName = "decimal(18, 2)")] // Định dạng tiền tệ trong SQL
        public decimal TotalAmount { get; set; }

        // Liên kết với Table
        public int? TableId { get; set; }
        [ForeignKey("TableId")]
        public virtual Table? Table { get; set; }

        // Liên kết với User
        public int? UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        // Danh sách các món đã đặt trước
        public virtual List<PreOrderItem> PreOrderItems { get; set; } = new List<PreOrderItem>();
    }
}