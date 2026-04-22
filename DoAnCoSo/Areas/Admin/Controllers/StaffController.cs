using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnCoSo.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class StaffController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public StaffController(UserManager<User> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Sử dụng UserManager để lấy danh sách theo Role cực nhanh
            var staffMembers = await _userManager.GetUsersInRoleAsync("Staff");
            return View(staffMembers);
        }
        // 2. Thêm nhân viên mới (Get)
        public IActionResult Create() => View();

        // 3. Thêm nhân viên mới (Post)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string fullName, string phoneNumber, string password)
        {
            if (ModelState.IsValid)
            {
                var user = new User
                {
                    UserName = phoneNumber, // Dùng SĐT làm tên đăng nhập
                    PhoneNumber = phoneNumber,
                    FullName = fullName,
                    CreatedAt = DateTime.Now,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Staff");
                    TempData["Success"] = "Đã cấp tài khoản nhân viên thành công!";
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            return View();
        }

        // 4. Xóa nhân viên (Sử dụng AJAX để xóa cho hiện đại)
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) return Json(new { success = false, message = "Không tìm thấy nhân viên" });

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded) return Json(new { success = true });

            return Json(new { success = false, message = "Lỗi khi xóa dữ liệu" });
        }
    }
}
