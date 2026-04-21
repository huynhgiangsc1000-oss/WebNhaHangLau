using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoAnCoSo.Models
{
    public class CartItem
    {
        [Key]
        public int Id { get; set; }

        public int ProductId { get; set; }

        public int TableId { get; set; }

        public int Quantity { get; set; }

        // Bổ sung thuộc tính Product để sửa lỗi "does not contain a definition for 'Product'"
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        // Nếu View của bạn gọi item.ImagePath, hãy tạo một thuộc tính "bắc cầu" từ Product sang
        [NotMapped] // Không tạo cột này trong Database
        public string ImagePath => Product?.ImagePath;

        [NotMapped]
        public string ProductName => Product?.ProductName;

        [NotMapped]
        public decimal Price => Product?.Price ?? 0;

        [NotMapped]
        public decimal TotalPrice => Price * Quantity;
    }
}