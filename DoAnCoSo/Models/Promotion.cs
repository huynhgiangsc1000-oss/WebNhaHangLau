using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoAnCoSo.Models
{
    public class Promotion
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tiêu đề không được để trống")]
        [Display(Name = "Tiêu đề ưu đãi")]
        public string Title { get; set; }

        [Display(Name = "Mô tả chi tiết")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Mã giảm giá là bắt buộc")]
        [Display(Name = "Mã Coupon")]
        public string CouponCode { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá trị giảm")]
        [Range(0, 100, ErrorMessage = "Giảm giá phải từ 0 đến 100%")]
        [Display(Name = "Phần trăm giảm (%)")]
        public decimal DiscountValue { get; set; }

        [Display(Name = "Hình ảnh banner")]
        public string? ImageUrl { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu")]
        [Display(Name = "Ngày bắt đầu")]
        public DateTime StartDate { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc")]
        [Display(Name = "Ngày kết thúc")]
        public DateTime EndDate { get; set; } = DateTime.Now.AddDays(7);

        [Display(Name = "Trạng thái hoạt động")]
        public bool IsActive { get; set; } = true;

        // --- LIÊN KẾT DỮ LIỆU ---

        // Một chương trình ưu đãi có thể được áp dụng trong nhiều đơn hàng khác nhau
        public virtual ICollection<Order>? Orders { get; set; }
    }
}