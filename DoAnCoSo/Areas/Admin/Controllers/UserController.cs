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
        private readonly RoleManager<IdentityRole<int>> _roleManager; // Thêm dòng này
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context, RoleManager<IdentityRole<int>> roleManager, UserManager<User> userManager)
        {
            _userManager = userManager;
            _roleManager = roleManager; // Gán giá trị vào biến private
            _context = context;
        }

        // 1. Hiển thị danh sách khách hàng
        public async Task<IActionResult> Index()
        {
            // 1. Lấy ID của vai trò Admin để lọc nhanh trong database
            var adminRole = await _roleManager.FindByNameAsync("Admin");
            var adminId = adminRole?.Id;

            // 2. Truy vấn danh sách User:
            // - Include(u => u.Rank): Để hiển thị được "Bậc Đồng", "Bậc Bạc"...
            // - Where: Lọc bỏ những user có RoleId trùng với Admin
            var customersOnly = await _context.Users
                .Include(u => u.Rank)
                .Where(u => !_context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == adminId))
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return View(customersOnly);
        }

        // 2. Xem chi tiết khách hàng và lịch sử đơn hàng
        public async Task<IActionResult> Details(int id)
        {
            var user = await _context.Users
                .Include(u => u.Rank)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            // Lấy lịch sử đơn hàng của khách
            ViewBag.Orders = await _context.Orders
                .Where(o => o.UserId == id)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(user);
        }

        // 3. Chỉnh sửa thông tin khách hàng (Họ tên, Điểm, Hạng)
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            ViewBag.RankId = new SelectList(_context.Set<Rank>(), "RankId", "RankName", user.RankId);
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, User updatedUser)
        {
            if (id != updatedUser.Id) return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (ModelState.IsValid)
            {
                user.FullName = updatedUser.FullName;
                user.Points = updatedUser.Points;
                user.RankId = updatedUser.RankId;
                user.PhoneNumber = updatedUser.PhoneNumber;

                _context.Update(user);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cập nhật thông tin khách hàng thành công!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.RankId = new SelectList(_context.Set<Rank>(), "RankId", "RankName", updatedUser.RankId);
            return View(updatedUser);
        }

        // 4. Xóa khách hàng (Lưu ý: Cần cân nhắc ràng buộc với dữ liệu Đơn hàng)
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                // Kiểm tra nếu khách có đơn hàng thì không cho xóa hoặc xử lý khác
                bool hasOrders = await _context.Orders.AnyAsync(o => o.UserId == id);
                if (hasOrders)
                {
                    TempData["Error"] = "Không thể xóa khách hàng này vì đã có lịch sử đơn hàng!";
                    return RedirectToAction(nameof(Index));
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa khách hàng thành công.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
