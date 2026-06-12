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
        // Thêm vào Index trong UserController
        public async Task<IActionResult> Index(string searchString, int? rankId, int page = 1)
        {
            int pageSize = 10; // Thay đổi tùy số lượng bạn muốn hiện mỗi trang
            var query = _context.Users.Include(u => u.Rank).AsQueryable();

            // Lọc tìm kiếm và hạng (như cũ)
            if (!string.IsNullOrEmpty(searchString))
                query = query.Where(u => u.FullName.Contains(searchString) || u.PhoneNumber.Contains(searchString));
            if (rankId.HasValue)
                query = query.Where(u => u.RankId == rankId);

            int totalItems = await query.CountAsync();
            var customers = await query.OrderByDescending(u => u.Points)
                                       .Skip((page - 1) * pageSize)
                                       .Take(pageSize)
                                       .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.SearchString = searchString;
            ViewBag.RankId = rankId;
            ViewBag.Ranks = new SelectList(_context.Ranks, "RankId", "RankName", rankId);

            return View(customers);
        }

        // Thêm hàm Khóa/Mở khóa
        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> ToggleLock(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.IsLocked = !user.IsLocked;
                await _context.SaveChangesAsync();
                TempData["Success"] = user.IsLocked ? "Đã khóa tài khoản." : "Đã mở khóa tài khoản.";
            }
            return RedirectToAction(nameof(Index));
        }

        // 2. CHI TIẾT KHÁCH HÀNG (Lịch sử giao dịch & Thống kê)
        public async Task<IActionResult> Details(int id, int page = 1)
        {
            int pageSize = 10;

            // Lấy thông tin user kèm hạng thành viên
            var user = await _context.Users
                .Include(u => u.Rank)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            // Lấy danh sách đơn hàng của khách, bao gồm cả chi tiết món ăn
            var query = _context.Orders
                .Include(o => o.OrderDetails) // Lấy thông tin món ăn trong đơn
                .ThenInclude(od => od.Product)
                .Where(o => o.UserId == id)
                .OrderByDescending(o => o.OrderDate);

            int totalCount = await query.CountAsync();
            var orders = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.Orders = orders;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.UserId = id;

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
            var ranks = await _context.Set<Rank>().OrderByDescending(r => r.RequiredPoints).ToListAsync();

            foreach (var u in users)
            {
                var matchedRank = ranks.FirstOrDefault(r => u.Points >= r.RequiredPoints);
                if (matchedRank != null && u.RankId != matchedRank.RankId)
                    u.RankId = matchedRank.RankId;
            }

            await _context.SaveChangesAsync(); // Lưu 1 lần duy nhất cho toàn bộ danh sách
            TempData["Success"] = "Đã cập nhật xong hạng cho tất cả khách hàng!";
            return RedirectToAction(nameof(Index));
        }

        // 5. XÓA KHÁCH HÀNG
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            // Kiểm tra xem khách đã có đơn hàng nào chưa
            bool hasOrders = await _context.Orders.AnyAsync(o => o.UserId == id);

            // Trong UserController.cs
            if (hasOrders)
            {
                // Đổi từ TempData["Error"] sang TempData["Warning"] để dễ phân loại
                TempData["Warning"] = "Khách hàng này đã có lịch sử giao dịch, không thể xóa!";
                return RedirectToAction(nameof(Index));
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa khách hàng thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}