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

        public int UserId { get; set; } // Đảm bảo có UserId nếu bạn đang phân tách giỏ hàng

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        // --- BỔ SUNG DÒNG NÀY ĐỂ HẾT LỖI ---
        [ForeignKey("TableId")]
        public virtual Table Table { get; set; }
        // ------------------------------------

        [NotMapped]
        public string ImagePath => Product?.ImagePath;

        [NotMapped]
        public string ProductName => Product?.ProductName;

        [NotMapped]
        public decimal Price => Product?.Price ?? 0;

        [NotMapped]
        public decimal TotalPrice => Price * Quantity;
    }
}