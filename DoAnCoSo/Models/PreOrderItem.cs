namespace DoAnCoSo.Models
{
    public class PreOrderItem
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }

        // Navigation properties (giúp EF Core nhận diện quan hệ)
        public Booking Booking { get; set; }
        public Product Product { get; set; }
    }
}