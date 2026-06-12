using DoAnCoSo.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class PreOrderItem
{
    [Key]
    public int Id { get; set; }
    public int BookingId { get; set; }
    [ForeignKey("BookingId")]
    public virtual Booking? Booking { get; set; }

    public int ProductId { get; set; }
    [ForeignKey("ProductId")]
    public virtual Product? Product { get; set; }

    public int Quantity { get; set; }
    public decimal PriceAtOrder { get; set; } // Giá chốt tại lúc đặt
}