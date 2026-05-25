using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DoAnCoSo.Models;

namespace DoAnCoSo.Data
{
    // Sử dụng User model tùy chỉnh và khóa chính kiểu int
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<int>, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Rank> Ranks { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Table> Tables { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Promotion> Promotions { get; set; }
        public DbSet<PreOrderItem> PreOrderItems { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- Cấu hình Identity của bạn ---
            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<IdentityRole<int>>().ToTable("Roles");
            modelBuilder.Entity<IdentityUserRole<int>>().ToTable("UserRoles");
            modelBuilder.Entity<IdentityUserClaim<int>>().ToTable("UserClaims");
            modelBuilder.Entity<IdentityUserLogin<int>>().ToTable("UserLogins");
            modelBuilder.Entity<IdentityRoleClaim<int>>().ToTable("RoleClaims");
            modelBuilder.Entity<IdentityUserToken<int>>().ToTable("UserTokens");

            // --- CẤU HÌNH PRE-ORDER (Đặt món trước) ---

            // Thiết lập mối quan hệ 1-Nhiều giữa Booking và PreOrderItem
            modelBuilder.Entity<PreOrderItem>()
                .HasOne(p => p.Booking)
                .WithMany(b => b.PreOrderItems)
                .HasForeignKey(p => p.BookingId)
                .OnDelete(DeleteBehavior.Cascade); // Xóa Booking sẽ xóa sạch các món đã chọn

            // Thiết lập quan hệ giữa PreOrderItem và Product (để lấy giá/tên món)
            modelBuilder.Entity<PreOrderItem>()
                .HasOne(p => p.Product)
                .WithMany()
                .HasForeignKey(p => p.ProductId)
                .OnDelete(DeleteBehavior.Restrict); // Không cho xóa món nếu vẫn còn đơn PreOrder
        }
    }
}