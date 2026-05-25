using DoAnCoSo.Data;
using DoAnCoSo.Models;
using DoAnCoSo.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnCoSo.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class AccountController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ApplicationDbContext _context;

        public AccountController(UserManager<User> userManager,
                                 SignInManager<User> signInManager,
                                 ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        [HttpGet]
        public IActionResult Register(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var defaultRank = await _context.Ranks.FirstOrDefaultAsync(r => r.RankName == "Đồng");

                var user = new User
                {
                    UserName = model.Phone,
                    PhoneNumber = model.Phone,
                    FullName = model.FullName,
                    RankId = defaultRank?.RankId
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Customer");
                    await _signInManager.SignInAsync(user, isPersistent: false);

                    // --- BỔ SUNG: Đồng bộ đơn đặt bàn cho khách vừa Đăng ký tại bàn ---
                    await LinkGuestBookingToUserAsync(user.Id);

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    return RedirectToAction("Index", "Home", new { area = "Customer" });
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Phone, model.Password, model.RememberMe, false);

                if (result.Succeeded)
                {
                    // Khôi phục session bàn từ Cookie (nếu có)
                    var tableIdFromCookie = Request.Cookies["SavedTableId"];
                    if (!string.IsNullOrEmpty(tableIdFromCookie))
                    {
                        HttpContext.Session.SetString("TableId", tableIdFromCookie);
                    }

                    // --- BỔ SUNG: Đồng bộ đơn đặt bàn cho khách vừa Đăng nhập tại bàn ---
                    var user = await _userManager.FindByNameAsync(model.Phone);
                    if (user != null)
                    {
                        await LinkGuestBookingToUserAsync(user.Id);
                    }

                    // Điều hướng
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }

                    var roles = await _userManager.GetRolesAsync(user);
                    if (roles.Contains("Admin"))
                        return RedirectToAction("Index", "Home", new { area = "Admin" });

                    return RedirectToAction("Index", "Home", new { area = "Customer" });
                }
                ModelState.AddModelError("", "Số điện thoại hoặc mật khẩu không đúng.");
            }
            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.Remove("TableId");
            return RedirectToAction("Index", "Home", new { area = "Customer" });
        }

        // --- HÀM PHỤ TRỢ: Tự động kết nối đơn đặt bàn vãng lai vào tài khoản thành viên ---
        private async Task LinkGuestBookingToUserAsync(int userId)
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];

            if (!string.IsNullOrEmpty(tableIdStr))
            {
                int tableId = int.Parse(tableIdStr);

                // Tìm đơn đặt bàn tại đúng số bàn này, trạng thái đã CheckedIn nhưng UserId vẫn đang NULL
                var guestBooking = await _context.Bookings
                    .FirstOrDefaultAsync(b => b.TableId == tableId && b.Status == "CheckedIn" && b.UserId == null);

                if (guestBooking != null)
                {
                    guestBooking.UserId = userId;
                    await _context.SaveChangesAsync();
                }
            }
        }
    }
}