using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DoAnCoSo.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public OrderController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 1. Trang hiển thị chi tiết đơn hàng
        public async Task<IActionResult> OrderDetails(int id)
        {
            // Lấy ID người dùng an toàn
            var userIdString = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userIdString)) return Challenge();
            int userId = int.Parse(userIdString);

            var order = await _context.Orders
                .Include(o => o.Table)           // Nạp thông tin bàn để lấy TableName
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.User)
                    .ThenInclude(u => u.Rank)
                .FirstOrDefaultAsync(m => m.OrderId == id);

            if (order == null)
            {
                return NotFound();
            }

            // Bảo mật: Chỉ cho phép người sở hữu đơn hàng xem
            if (order.UserId != userId)
            {
                return Forbid();
            }

            // Truyền TableName ra View qua ViewBag để hiển thị tiêu đề đẹp hơn nếu cần
            ViewBag.TableName = order.Table?.TableName ?? "N/A";

            return View(order);
        }

        // 2. Trang danh sách lịch sử đơn hàng của User
        public async Task<IActionResult> History()
        {
            var userIdString = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userIdString)) return Challenge();
            int userId = int.Parse(userIdString);

            // Lấy danh sách đơn hàng kèm thông tin bàn
            var orders = await _context.Orders
                .Include(o => o.Table) // Quan trọng: Để lấy TableName hiển thị thay vì ID
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }
    }
}