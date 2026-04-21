using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoAnCoSo.Models
{
    public class Product
    {
        [Key]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Tên món ăn không được để trống")]
        public string ProductName { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá phải lớn hơn 0")]
        public decimal Price { get; set; }

        public string? ImagePath { get; set; }

        public string? Description { get; set; }

        public bool IsAvailable { get; set; } = true;

        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        // Đảm bảo có thuộc tính này để gọi .Include(p => p.Category)
        public virtual Category? Category { get; set; }

        // Liên kết với chi tiết đơn hàng (nếu cần thống kê món bán chạy)
        public virtual ICollection<OrderDetail>? OrderDetails { get; set; }
    }
}