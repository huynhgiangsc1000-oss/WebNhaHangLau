using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Http;

namespace DoAnCoSo.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class TableController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TableController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. Hiển thị sơ đồ bàn cho khách hàng xem trạng thái (Xanh/Đỏ)
        public async Task<IActionResult> Index()
        {
            // Sắp xếp bàn theo tên để khách dễ tìm
            var tables = await _context.Tables.OrderBy(t => t.TableName).ToListAsync();
            return View(tables);
        }

        // 2. Xử lý khi khách chọn bàn thủ công từ sơ đồ hoặc quét mã QR
        [HttpGet]
        public async Task<IActionResult> AccessTable(int id)
        {
            // Nếu chưa đăng nhập, bắt đi đăng nhập và kèm theo ReturnUrl quay lại chính hàm này
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account", new
                {
                    area = "Customer",
                    returnUrl = Url.Action("AccessTable", "Table", new { area = "Customer", id = id })
                });
            }

            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound();

            // Lưu bàn vào Session & Cookie
            HttpContext.Session.SetString("TableId", id.ToString());
            CookieOptions option = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(1),
                HttpOnly = true,
                IsEssential = true
            };
            Response.Cookies.Append("SavedTableId", id.ToString(), option);

            return RedirectToAction("Index", "Menu");
        }
        public async Task<IActionResult> CurrentTable()
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];

            if (string.IsNullOrEmpty(tableIdStr))
            {
                return RedirectToAction(nameof(Index));
            }

            int tableId = int.Parse(tableIdStr);
            var table = await _context.Tables.FindAsync(tableId);

            return View(table);
        }
    }
}