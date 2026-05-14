using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnCoSo.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Staff")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public HomeController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            // --- THỐNG KÊ CƠ BẢN ---
            ViewBag.TotalProducts = await _context.Products.CountAsync();

            // Lấy danh sách khách hàng (Lọc bỏ Admin & Staff)
            var allUsers = await _userManager.Users.ToListAsync();
            int customerCount = 0;
            foreach (var user in allUsers)
            {
                if (!await _userManager.IsInRoleAsync(user, "Admin") && !await _userManager.IsInRoleAsync(user, "Staff"))
                    customerCount++;
            }
            ViewBag.TotalUsers = customerCount;

            // --- THỐNG KÊ DOANH THU (Đã cập nhật logic Discount) ---

            // 1. Tổng doanh thu THỰC THU (Số tiền khách đã trả sau khi giảm giá)
            ViewBag.TotalRevenue = await _context.Orders
                .Where(o => o.Status == Order.StatusCompleted)
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            // 2. Tổng số tiền đã GIẢM GIÁ cho khách (Để Admin biết ngân sách khuyến mãi đã chi)
            ViewBag.TotalDiscount = await _context.Orders
                .Where(o => o.Status == Order.StatusCompleted)
                .SumAsync(o => (decimal?)o.DiscountAmount) ?? 0;

            // 3. Tổng số đơn hàng thành công
            ViewBag.TotalOrders = await _context.Orders
                .CountAsync(o => o.Status == Order.StatusCompleted);

            // 4. Doanh thu 12 tháng cho biểu đồ
            var currentYear = DateTime.Now.Year;
            var monthlyRevenue = new List<decimal>();
            for (int month = 1; month <= 12; month++)
            {
                var total = await _context.Orders
                    .Where(o => o.OrderDate.Month == month
                            && o.OrderDate.Year == currentYear
                            && o.Status == Order.StatusCompleted)
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
                monthlyRevenue.Add(total);
            }
            ViewBag.MonthlyRevenue = monthlyRevenue;

            // --- DANH SÁCH GẦN ĐÂY ---

            // Lấy 10 đơn hàng mới nhất (Include thêm Promotion để hiện mã giảm giá nếu có)
            ViewBag.RecentOrders = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Table)
                .Include(o => o.Promotion) // Thêm dòng này để hiển thị voucher trên Dashboard
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .ToListAsync();

            // --- THỐNG KÊ ĐẶT BÀN ---
            ViewBag.PendingBookings = await _context.Bookings.CountAsync(b => b.Status == "Pending");

            var today = DateTime.Today;
            ViewBag.TodayBookings = await _context.Bookings
                .CountAsync(b => b.BookingDate.Date == today && b.Status == "Confirmed");

            ViewBag.RecentBookings = await _context.Bookings
                .OrderByDescending(b => b.CreatedAt)
                .Take(5)
                .ToListAsync();

            return View();
        }
    }
}