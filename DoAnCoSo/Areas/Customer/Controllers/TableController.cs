using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace DoAnCoSo.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class TableController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public TableController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /// <summary>
        /// Hiển thị sơ đồ nhà hàng
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var tables = await _context.Tables
                .Include(t => t.Bookings)
                .OrderBy(t => t.TableName)
                .ToListAsync();
            return View(tables);
        }

        /// <summary>
        /// Xử lý khi khách quét mã QR hoặc nhấn chọn bàn
        /// </summary>
        [HttpGet]
        [HttpGet]
        public async Task<IActionResult> AccessTable(int id)
        {
            if (!User.Identity.IsAuthenticated)
            {
                string returnUrl = Url.Action("AccessTable", "Table", new { area = "Customer", id = id });
                return RedirectToAction("Login", "Account", new { area = "Customer", returnUrl = returnUrl });
            }

            var user = await _userManager.GetUserAsync(User);
            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound();

            if (table.Status == "Occupied")
            {
                var hasActiveOrder = await _context.Orders
                    .AnyAsync(o => o.TableId == id && o.UserId == user.Id && (o.Status == "Pending" || o.Status == "Processing"));

                var hasCheckedInBooking = await _context.Bookings
                    .AnyAsync(b => b.TableId == id && b.UserId == user.Id && b.Status == "CheckedIn");

                // Nếu bàn đỏ nhưng không phải của mình
                if (!hasActiveOrder && !hasCheckedInBooking)
                {
                    TempData["Error"] = "Bàn này đang được phục vụ khách khác!";
                    return RedirectToAction(nameof(Index));
                }
            }

            // Khách vãng lai quét bàn trống hoặc khách đặt quét bàn Reserved
            if (table.Status == "Empty" || table.Status == "Reserved")
            {
                table.Status = "Occupied";
                _context.Update(table);
                await _context.SaveChangesAsync();
            }

            // Thiết lập Session & Cookie
            HttpContext.Session.SetString("TableId", id.ToString());
            Response.Cookies.Append("SavedTableId", id.ToString(), new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddDays(1),
                HttpOnly = true,
                IsEssential = true
            });

            return RedirectToAction("Index", "Menu");
        }
        public async Task<IActionResult> CurrentTable()
        {
            var tableIdStr = HttpContext.Session.GetString("TableId") ?? Request.Cookies["SavedTableId"];

            if (string.IsNullOrEmpty(tableIdStr))
            {
                return RedirectToAction(nameof(Index));
            }

            if (int.TryParse(tableIdStr, out int tableId))
            {
                var table = await _context.Tables
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TableId == tableId);

                if (table != null) return View(table);
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Giải phóng bàn (Dùng khi khách muốn đổi bàn hoặc nhân viên dọn bàn)
        /// </summary>
        public async Task<IActionResult> ReleaseTable(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table != null)
            {
                // Kiểm tra xem còn hóa đơn nào chưa thanh toán tại bàn này không trước khi cho giải phóng
                var hasActiveOrder = await _context.Orders
                    .AnyAsync(o => o.TableId == id && o.Status != "Completed" && o.Status != "Cancelled");

                if (hasActiveOrder)
                {
                    TempData["Error"] = "Không thể giải phóng bàn khi chưa thanh toán hóa đơn!";
                    return RedirectToAction("Index", "Order");
                }

                table.Status = "Empty";
                _context.Update(table);
                await _context.SaveChangesAsync();
            }

            // Xóa dấu vết bàn trong trình duyệt khách
            HttpContext.Session.Remove("TableId");
            Response.Cookies.Delete("SavedTableId");

            return RedirectToAction(nameof(Index));
        }
    }
}