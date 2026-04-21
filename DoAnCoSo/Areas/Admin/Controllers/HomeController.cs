using DoAnCoSo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnCoSo.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        public HomeController(ApplicationDbContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            // 1. Lấy các con số tổng quát cho 4 thẻ Top
            ViewBag.TotalProducts = await _context.Products.CountAsync();
            ViewBag.TotalOrders = await _context.Orders.CountAsync();
            ViewBag.TotalRevenue = await _context.Orders.SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            ViewBag.TotalUsers = await _context.Users.CountAsync();

            // 2. Lấy dữ liệu doanh thu 12 tháng của năm hiện tại cho biểu đồ
            var currentYear = DateTime.Now.Year;
            var monthlyRevenue = new List<decimal>();
            for (int month = 1; month <= 12; month++)
            {
                var total = await _context.Orders
                    .Where(o => o.OrderDate.Month == month && o.OrderDate.Year == currentYear)
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
                monthlyRevenue.Add(total);
            }
            ViewBag.MonthlyRevenue = monthlyRevenue;

            // 3. Lấy toàn bộ đơn hàng mới nhất để hiện lên bảng (Xóa .Take(5) theo ý bạn)
            ViewBag.RecentOrders = await _context.Orders
                .Include(o => o.User)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View();
        }
    }
}