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
        public async Task<IActionResult> AccessTable(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound();

            // Lưu vào Session (dạng String để khớp với CartController)
            HttpContext.Session.SetString("TableId", id.ToString());

            // Lưu vào Cookie để ghi nhớ nếu khách đóng trình duyệt
            CookieOptions option = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(1),
                HttpOnly = true,
                IsEssential = true
            };
            Response.Cookies.Append("SavedTableId", id.ToString(), option);

            // Sau khi chọn bàn thành công, chuyển hướng đến Menu
            return RedirectToAction("Index", "Menu");
        }

        // 3. Xem thông tin bàn mà trình duyệt này đang ghi nhớ
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