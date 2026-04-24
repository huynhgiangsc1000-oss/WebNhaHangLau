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

                    // Kiểm tra và điều hướng về returnUrl nếu có (ví dụ trang Menu kèm tableId)
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
            // Lưu returnUrl vào ViewData để truyền sang form đăng nhập trong View
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [HttpPost]
        // Areas/Customer/Controllers/AccountController.cs
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Phone, model.Password, model.RememberMe, false);

                if (result.Succeeded)
                {
                    // A. Khôi phục session bàn từ Cookie (nếu có)
                    var tableIdFromCookie = Request.Cookies["SavedTableId"];
                    if (!string.IsNullOrEmpty(tableIdFromCookie))
                    {
                        HttpContext.Session.SetString("TableId", tableIdFromCookie);
                    }

                    // B. Điều hướng quan trọng:
                    // Nếu có returnUrl (ví dụ: /Customer/Table/AccessTable/5), hãy quay lại đó
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }

                    // Nếu không có returnUrl thì mới phân quyền về Home Admin/Customer
                    var user = await _userManager.FindByNameAsync(model.Phone);
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

            // Lưu ý: Không nên xóa "SavedTableId" trong Cookie ở đây 
            // để khi họ đăng nhập lại vẫn còn số bàn cũ.
            HttpContext.Session.Remove("TableId");

            return RedirectToAction("Index", "Home", new { area = "Customer" });
        }
    }
}