using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoAnCoSo.Models
{
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Tên loại không được để trống")]
        public string CategoryName { get; set; }

        public string? IconPath { get; set; }

        // --- PHẦN THÊM MỚI ĐỂ PHÂN CẤP ---

        // ID của loại cha (Nếu null thì đây là loại cấp cao nhất như "Món lẻ" hoặc "Set Menu")
        public int? ParentId { get; set; }

        [ForeignKey("ParentId")]
        public virtual Category? ParentCategory { get; set; }

        // Danh sách các loại con thuộc về loại này
        public virtual ICollection<Category>? SubCategories { get; set; }

        // --------------------------------

        // Quan hệ 1-N với Sản phẩm
        public virtual ICollection<Product>? Products { get; set; }
    }
}