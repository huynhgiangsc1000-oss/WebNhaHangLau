using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DoAnCoSo.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole<int>> _roleManager;
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context, RoleManager<IdentityRole<int>> roleManager, UserManager<User> userManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // 1. DANH SÁCH KHÁCH HÀNG (Loại bỏ Admin và Staff)
        public async Task<IActionResult> Index(string searchString, int? rankId)
        {
            // Lấy ID của các vai trò cần loại bỏ
            var adminRole = await _roleManager.FindByNameAsync("Admin");
            var staffRole = await _roleManager.FindByNameAsync("Staff");

            var adminId = adminRole?.Id;
            var staffId = staffRole?.Id;

            // Truy vấn: Lấy những User KHÔNG nằm trong bảng UserRoles với RoleId là Admin hoặc Staff
            var query = _context.Users.Include(u => u.Rank).AsQueryable();

            if (adminId.HasValue)
                query = query.Where(u => !_context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == adminId));

            if (staffId.HasValue)
                query = query.Where(u => !_context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == staffId));

            // Tìm kiếm theo tên hoặc SĐT
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(u => u.FullName.Contains(searchString) || u.PhoneNumber.Contains(searchString));
            }

            // Lọc theo hạng
            if (rankId.HasValue)
            {
                query = query.Where(u => u.RankId == rankId);
            }

            var customers = await query.OrderByDescending(u => u.Points).ToListAsync();

            ViewBag.Ranks = new SelectList(_context.Ranks, "RankId", "RankName");
            return View(customers);
        }

        // 2. CHI TIẾT KHÁCH HÀNG (Lịch sử giao dịch & Thống kê)
        public async Task<IActionResult> Details(int id)
        {
            var user = await _context.Users
                .Include(u => u.Rank)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            // Lấy lịch sử đơn hàng
            var orders = await _context.Orders
                .Where(o => o.UserId == id)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            // Thống kê doanh thu từ khách này (chỉ tính đơn đã thanh toán)
            ViewBag.TotalSpent = orders.Where(o => o.Status == "Paid").Sum(o => o.TotalAmount);
            ViewBag.OrderCount = orders.Count;

            // Tìm món ăn khách thích nhất
            var topProduct = await _context.OrderDetails
                .Include(od => od.Order)
                .Include(od => od.Product)
                .Where(od => od.Order.UserId == id)
                .GroupBy(od => od.Product.ProductName)
                .Select(g => new { Name = g.Key, Count = g.Sum(x => x.Quantity) })
                .OrderByDescending(x => x.Count)
                .FirstOrDefaultAsync();

            ViewBag.TopProduct = topProduct?.Name ?? "Chưa có dữ liệu";
            ViewBag.Orders = orders;

            return View(user);
        }

        // 3. CHỈNH SỬA THÔNG TIN (Admin can thiệp điểm/hạng)
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            ViewBag.RankId = new SelectList(_context.Set<Rank>(), "RankId", "RankName", user.RankId);
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(User updatedUser)
        {
            var user = await _context.Users.FindAsync(updatedUser.Id);
            if (user == null) return NotFound();

            if (ModelState.IsValid)
            {
                user.FullName = updatedUser.FullName;
                user.PhoneNumber = updatedUser.PhoneNumber;
                user.Points = updatedUser.Points;
                user.RankId = updatedUser.RankId;

                await _context.SaveChangesAsync();
                TempData["Success"] = "Cập nhật thông tin thành công!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.RankId = new SelectList(_context.Set<Rank>(), "RankId", "RankName", updatedUser.RankId);
            return View(updatedUser);
        }

        // 4. LOGIC TỰ ĐỘNG CẬP NHẬT HẠNG CHO TOÀN BỘ USER
        [HttpPost]
        public async Task<IActionResult> UpdateAllRanks()
        {
            var users = await _context.Users.ToListAsync();
            // Lấy danh sách hạng sắp xếp theo điểm yêu cầu giảm dần
            var ranks = await _context.Set<Rank>().OrderByDescending(r => r.RequiredPoints).ToListAsync();

            int count = 0;
            foreach (var user in users)
            {
                // Tìm hạng cao nhất mà user đạt đủ điểm
                var matchedRank = ranks.FirstOrDefault(r => user.Points >= r.RequiredPoints);
                if (matchedRank != null && user.RankId != matchedRank.RankId)
                {
                    user.RankId = matchedRank.RankId;
                    count++;
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã kiểm tra và cập nhật hạng cho {count} khách hàng!";
            return RedirectToAction(nameof(Index));
        }

        // 5. XÓA KHÁCH HÀNG
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                // Ràng buộc: Không xóa nếu có đơn hàng
                bool hasOrders = await _context.Orders.AnyAsync(o => o.UserId == id);
                if (hasOrders)
                {
                    TempData["Error"] = "LỖI: Khách hàng đã có lịch sử đơn hàng, không thể xóa!";
                    return RedirectToAction(nameof(Index));
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa tài khoản thành công.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}