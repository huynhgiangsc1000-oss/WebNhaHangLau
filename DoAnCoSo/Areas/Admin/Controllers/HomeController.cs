using DoAnCoSo.Data;
using DoAnCoSo.Models; // Quan trọng: Để nhận diện class User tùy chỉnh
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
        // SỬA: Dùng User (class của bạn) thay vì IdentityUser
        private readonly UserManager<User> _userManager;

        public HomeController(
            ApplicationDbContext context,
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Lấy dữ liệu nghiệp vụ (Sản phẩm, Đơn hàng, Doanh thu)
            ViewBag.TotalProducts = await _context.Products.CountAsync();
            ViewBag.TotalOrders = await _context.Orders.CountAsync();
            ViewBag.TotalRevenue = await _context.Orders.SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            // 2. Đếm số lượng Khách hàng (Lọc bỏ Admin và Staff)
            // Lấy danh sách từ UserManager<User> đã sửa ở trên
            var allUsers = await _userManager.Users.ToListAsync();
            int customerCount = 0;

            foreach (var user in allUsers)
            {
                // Kiểm tra vai trò dựa trên Identity
                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                var isStaff = await _userManager.IsInRoleAsync(user, "Staff");

                if (!isAdmin && !isStaff)
                {
                    customerCount++;
                }
            }
            ViewBag.TotalUsers = customerCount;

            // 3. Lấy dữ liệu doanh thu 12 tháng của năm hiện tại
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

            // 4. Lấy danh sách đơn hàng mới nhất (Hiện lên bảng Dashboard)
            ViewBag.RecentOrders = await _context.Orders
                .Include(o => o.User)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View();
        }
    }
}