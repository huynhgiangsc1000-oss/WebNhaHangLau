using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnCoSo.Areas.Admin.Controllers
{
        // 1. Hiển thị danh sách đơn hàng
        [Area("Admin")]
        [Authorize(Roles = "Admin")] // Đảm bảo chỉ Admin truy cập
        public class OrderController : Controller
        {
            private readonly ApplicationDbContext _context;
            public OrderController(ApplicationDbContext context) => _context = context;

            public async Task<IActionResult> Index()
            {
                var orders = await _context.Orders
                    .Include(o => o.Table)
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();
                return View(orders);
            }

            public async Task<IActionResult> GetDetails(int id)
            {
                var order = await _context.Orders
                    .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                    .Include(o => o.Table)
                    .FirstOrDefaultAsync(o => o.OrderId == id);

                if (order == null) return NotFound();
                return PartialView("_OrderDetailModal", order);
            }

            [HttpPost]
            public async Task<IActionResult> UpdateStatus(int id, string status)
            {
                var order = await _context.Orders.Include(o => o.Table).FirstOrDefaultAsync(o => o.OrderId == id);
                if (order == null) return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });

                try
                {
                    order.Status = status;

                    // TỰ ĐỘNG GIẢI PHÓNG BÀN KHI THANH TOÁN XONG
                    if (status == "Paid" && order.Table != null)
                    {
                        order.Table.Status = "Empty";
                    }

                    await _context.SaveChangesAsync();
                    return Json(new { success = true });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = ex.Message });
                }
            }
        }
    }