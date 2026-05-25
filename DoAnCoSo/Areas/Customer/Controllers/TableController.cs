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
        public async Task<IActionResult> AccessTable(int id, string checkInCode = null)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound();

            if (table.Status == "Occupied")
            {
                // 1. Kiểm tra xem có đơn hàng nào đang hoạt động (đang ăn) tại bàn này không
                var hasActiveOrder = await _context.Orders
                    .AnyAsync(o => o.TableId == id && o.Status != "Completed" && o.Status != "Cancelled");

                // 2. Kiểm tra xem bàn này có phải là bàn vừa được nhân viên check-in cho khách vãng lai không (UserId == null)
                var isGuestBookingCheckedIn = await _context.Bookings
                    .AnyAsync(b => b.TableId == id && b.Status == "CheckedIn" && b.UserId == null);

                // 3. LOGIC LỌC TRUY CẬP:
                // Nếu khách click trên sơ đồ (checkInCode rỗng)
                if (string.IsNullOrEmpty(checkInCode))
                {
                    // Nếu bàn đã có hóa đơn đang chạy (khách khác đang ăn) 
                    // HOẶC bàn này KHÔNG CÓ đơn check-in vãng lai nào chờ sẵn thì mới chặn
                    if (hasActiveOrder && !isGuestBookingCheckedIn)
                    {
                        TempData["Error"] = "Bàn này hiện đang có khách. Vui lòng liên hệ nhân viên!";
                        return RedirectToAction("Index");
                    }
                }
                else // Nếu khách truy cập thông qua đường dẫn link QR từ Email (Có kèm checkInCode)
                {
                    var isCorrectBooking = await _context.Bookings
                        .AnyAsync(b => b.TableId == id && b.CheckInCode == checkInCode && b.Status == "CheckedIn");

                    if (!isCorrectBooking)
                    {
                        TempData["Error"] = "Mã xác nhận bàn ăn không hợp lệ hoặc đã hết hạn!";
                        return RedirectToAction("Index");
                    }

                    // Lưu mã checkInCode vào Session dự phòng
                    HttpContext.Session.SetString("GuestCheckInCode", checkInCode);
                }
            }

            // Cấp quyền tiếp cận bàn tạm thời (Lưu Session & Cookie)
            HttpContext.Session.SetString("TableId", id.ToString());
            Response.Cookies.Append("SavedTableId", id.ToString(), new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddDays(1)
            });

            // Chuyển hướng sang trang Menu
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