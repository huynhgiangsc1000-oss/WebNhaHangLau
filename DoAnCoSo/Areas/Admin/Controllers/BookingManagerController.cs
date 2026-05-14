using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Authorization;

namespace DoAnCoSo.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Staff")]
    public class BookingManagerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BookingManagerController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // --- LOGIC TỰ ĐỘNG GIẢI PHÓNG BÀN QUÁ HẠN ---
            // Định nghĩa: Quá 30 phút so với giờ hẹn mà trạng thái vẫn là "Confirmed" (đã gán bàn nhưng khách chưa check-in)
            var expiryTime = DateTime.Now.AddMinutes(-30);

            var expiredBookings = await _context.Bookings
                .Include(b => b.Table)
                .Where(b => b.Status == "Confirmed"
                         && b.BookingDate < expiryTime
                         && b.TableId != null)
                .ToListAsync();

            if (expiredBookings.Any())
            {
                foreach (var b in expiredBookings)
                {
                    // 1. Chuyển trạng thái đơn đặt sang "Expired" (hoặc "Cancelled" tùy bạn)
                    b.Status = "Expired";

                    // 2. Trả bàn về trạng thái "Empty" để có thể gán cho người khác
                    if (b.Table != null && b.Table.Status == "Reserved")
                    {
                        b.Table.Status = "Empty";
                    }
                }
                // Lưu tất cả thay đổi quét được vào Database
                await _context.SaveChangesAsync();
            }
            // --- KẾT THÚC LOGIC QUÉT QUÁ HẠN ---

            // Lấy danh sách booking để hiển thị ra View
            var bookings = await _context.Bookings
                .Include(b => b.Table)
                .Include(b => b.User)
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();

            // Lấy danh sách bàn trống ĐÃ BAO GỒM các bàn vừa được giải phóng ở trên
            ViewBag.AvailableTables = await _context.Tables
                .Where(t => t.Status == "Empty")
                .OrderBy(t => t.TableName)
                .ToListAsync();

            return View(bookings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmBooking(int bookingId, int tableId)
        {
            var booking = await _context.Bookings.FindAsync(bookingId);
            if (booking == null) return Json(new { success = false, message = "Không tìm thấy yêu cầu" });

            // 1. Cập nhật đơn đặt bàn
            booking.TableId = tableId;
            booking.Status = "Confirmed";

            // 2. Cập nhật trạng thái bàn sang "Reserved"
            var table = await _context.Tables.FindAsync(tableId);
            if (table != null)
            {
                table.Status = "Reserved";
                _context.Tables.Update(table);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelBooking(int id)
        {
            var booking = await _context.Bookings.Include(b => b.Table).FirstOrDefaultAsync(b => b.BookingId == id);
            if (booking == null) return Json(new { success = false, message = "Lỗi hệ thống" });

            // Nếu đơn này đã gán bàn, khi hủy phải trả bàn về "Empty"
            if (booking.TableId != null && booking.Table != null)
            {
                booking.Table.Status = "Empty";
            }

            booking.Status = "Cancelled";
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
        // GET: Admin/BookingManager/CheckIn
        public IActionResult CheckIn()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessCheckIn(string qrCode)
        {
            if (string.IsNullOrEmpty(qrCode))
            {
                return Json(new { success = false, message = "Mã không hợp lệ!" });
            }

            var booking = await _context.Bookings
                .Include(b => b.Table)
                .FirstOrDefaultAsync(b => b.CheckInCode == qrCode);

            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin đặt bàn với mã này." });
            }

            if (booking.Status == "CheckedIn")
            {
                return Json(new { success = false, message = "Mã này đã được Check-in trước đó rồi." });
            }

            if (booking.Status == "Cancelled" || booking.Status == "Expired")
            {
                return Json(new { success = false, message = "Đơn đặt bàn này đã bị hủy hoặc hết hạn." });
            }

            // Cập nhật trạng thái
            booking.Status = "CheckedIn";

            // Nếu đơn chưa có bàn (Pending) thì báo lỗi yêu cầu Admin gán bàn trước
            if (booking.TableId == null)
            {
                return Json(new { success = false, message = "Đơn này chưa được gán bàn. Vui lòng gán bàn tại danh sách quản lý trước!" });
            }

            // Cập nhật trạng thái bàn sang "Occupied" (Đang có khách ngồi)
            if (booking.Table != null)
            {
                booking.Table.Status = "Occupied";
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"Check-in thành công cho khách {booking.FullName}!",
                customer = booking.FullName,
                tableName = booking.Table?.TableName ?? "N/A"
            });
        }
    }
}