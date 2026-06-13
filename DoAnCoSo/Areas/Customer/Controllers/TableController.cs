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
        public async Task<IActionResult> AccessTable(int id, string checkInCode = null)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound();

            var userIdStr = _userManager.GetUserId(User);
            int? currentUserId = !string.IsNullOrEmpty(userIdStr) ? int.Parse(userIdStr) : null;

            // Tìm đơn hàng đang hoạt động tại bàn này
            var activeOrder = await _context.Orders
                .Include(o => o.Booking)
                .FirstOrDefaultAsync(o => o.TableId == id && o.Status != "Completed" && o.Status != "Cancelled");

            // Tìm booking đang CheckedIn tại bàn này
            var checkedInBooking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.TableId == id && b.Status == "CheckedIn");

            if (table.Status == "Occupied")
            {
                // Nếu khách truy cập thông qua đường dẫn link QR từ Email (Có kèm checkInCode)
                if (!string.IsNullOrEmpty(checkInCode))
                {
                    var bookingByCode = await _context.Bookings
                        .FirstOrDefaultAsync(b => b.TableId == id && b.CheckInCode == checkInCode && (b.Status == "CheckedIn" || b.Status == "Confirmed"));

                    if (bookingByCode == null)
                    {
                        TempData["Error"] = "Mã xác nhận bàn ăn không hợp lệ hoặc đã hết hạn!";
                        return RedirectToAction("Index");
                    }

                    // Lưu mã checkInCode vào Session dự phòng
                    HttpContext.Session.SetString("GuestCheckInCode", checkInCode);

                    // Nếu user đã đăng nhập, liên kết Booking và Order tương ứng với User này
                    if (currentUserId.HasValue)
                    {
                        if (bookingByCode.UserId == null)
                        {
                            bookingByCode.UserId = currentUserId.Value;
                            _context.Bookings.Update(bookingByCode);
                        }

                        var relatedOrder = await _context.Orders.FirstOrDefaultAsync(o => o.BookingId == bookingByCode.BookingId);
                        if (relatedOrder != null && relatedOrder.UserId == null)
                        {
                            relatedOrder.UserId = currentUserId.Value;
                            _context.Orders.Update(relatedOrder);
                        }

                        await _context.SaveChangesAsync();
                    }
                }
                else // Khách click trên sơ đồ hoặc quét mã QR bàn thông thường
                {
                    if (activeOrder != null)
                    {
                        // Nếu đơn hàng đang chạy thuộc về người khác -> chặn
                        if (activeOrder.UserId.HasValue)
                        {
                            if (currentUserId == null || activeOrder.UserId.Value != currentUserId.Value)
                            {
                                TempData["Error"] = "Bàn này hiện đang có khách. Vui lòng liên hệ nhân viên!";
                                return RedirectToAction("Index");
                            }
                        }
                        else
                        {
                            // Nếu đơn hàng chưa có UserId (khách vãng lai/check-in hộ)
                            if (currentUserId.HasValue)
                            {
                                activeOrder.UserId = currentUserId.Value;
                                _context.Orders.Update(activeOrder);

                                if (activeOrder.Booking != null && activeOrder.Booking.UserId == null)
                                {
                                    activeOrder.Booking.UserId = currentUserId.Value;
                                    _context.Bookings.Update(activeOrder.Booking);
                                }

                                await _context.SaveChangesAsync();
                            }
                        }
                    }
                    else if (checkedInBooking != null)
                    {
                        // Nếu chưa có hóa đơn nhưng có booking đang CheckedIn
                        if (checkedInBooking.UserId.HasValue)
                        {
                            if (currentUserId == null || checkedInBooking.UserId.Value != currentUserId.Value)
                            {
                                TempData["Error"] = "Bàn này hiện đang có khách. Vui lòng liên hệ nhân viên!";
                                return RedirectToAction("Index");
                            }
                        }
                        else
                        {
                            if (currentUserId.HasValue)
                            {
                                checkedInBooking.UserId = currentUserId.Value;
                                _context.Bookings.Update(checkedInBooking);
                                await _context.SaveChangesAsync();
                            }
                        }
                    }
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