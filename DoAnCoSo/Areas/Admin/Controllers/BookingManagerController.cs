using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnCoSo.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DoAnCoSo.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Staff")]
    public class BookingManagerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BookingManagerController(ApplicationDbContext context) => _context = context;

        public async Task<IActionResult> Index(string searchString, string statusFilter, string dateFilter = "Today")
        {
            // 1. TỰ ĐỘNG HỦY ĐƠN QUÁ HẠN (Sau 30 phút so với BookingDate)
            var expiryLimit = DateTime.Now.AddMinutes(-30);
            var expired = await _context.Bookings
                .Include(b => b.Table)
                .Where(b => b.Status == "Confirmed" && b.BookingDate < expiryLimit)
                .ToListAsync();

            foreach (var b in expired)
            {
                b.Status = "Cancelled";
                if (b.Table != null) b.Table.Status = "Empty";
            }
            await _context.SaveChangesAsync();

            // 2. Lấy dữ liệu hiển thị
            var query = _context.Bookings.Include(b => b.Table).Include(b => b.User).AsQueryable();

            // Bộ lọc thời gian & tìm kiếm (giữ nguyên logic cũ của bạn)
            var today = DateTime.Today;
            if (dateFilter == "Today") query = query.Where(b => b.BookingDate.Date == today);

            if (!string.IsNullOrEmpty(statusFilter)) query = query.Where(b => b.Status == statusFilter);
            if (!string.IsNullOrEmpty(searchString)) query = query.Where(b => b.FullName.Contains(searchString) || b.PhoneNumber.Contains(searchString));

            ViewBag.AvailableTables = await _context.Tables.Where(t => t.Status == "Empty").ToListAsync();
            return View(await query.OrderByDescending(b => b.BookingDate).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmBooking(int bookingId, int tableId)
        {
            var booking = await _context.Bookings
                .Include(b => b.PreOrderItems).ThenInclude(p => p.Product)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null) return Json(new { success = false });

            // Cập nhật trạng thái
            booking.TableId = tableId;
            booking.Status = "Confirmed";

            var table = await _context.Tables.FindAsync(tableId);
            if (table != null) table.Status = "Reserved";

            // Tự động tạo đơn hàng (Order) từ danh sách PreOrder
            if (booking.PreOrderItems != null && booking.PreOrderItems.Any())
            {
                var newOrder = new Order
                {
                    BookingId = bookingId,
                    TableId = tableId,
                    OrderDate = DateTime.Now,
                    Status = Order.StatusPending,
                    TotalAmount = booking.PreOrderItems.Sum(p => p.Quantity * p.Product.Price)
                };
                _context.Orders.Add(newOrder);
                await _context.SaveChangesAsync();

                foreach (var item in booking.PreOrderItems)
                {
                    _context.OrderDetails.Add(new OrderDetail
                    {
                        OrderId = newOrder.OrderId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.Product.Price
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ProcessCheckIn(string qrCode)
        {
            var booking = await _context.Bookings.Include(b => b.Table).FirstOrDefaultAsync(b => b.CheckInCode == qrCode);
            if (booking == null || booking.Status != "Confirmed")
                return Json(new { success = false, message = "Mã không hợp lệ hoặc đã quá hạn!" });

            booking.Status = "CheckedIn";
            if (booking.Table != null) booking.Table.Status = "Occupied";

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> FinishDining(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Table).FirstOrDefaultAsync(b => b.BookingId == bookingId);
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.BookingId == bookingId);

            if (booking == null) return Json(new { success = false });

            booking.Status = "Completed";
            if (booking.Table != null) booking.Table.Status = "Empty";
            if (order != null) order.Status = Order.StatusCompleted;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> CancelBooking(int id)
        {
            var booking = await _context.Bookings.Include(b => b.Table).FirstOrDefaultAsync(b => b.BookingId == id);
            if (booking != null)
            {
                booking.Status = "Cancelled";
                if (booking.Table != null) booking.Table.Status = "Empty";
                await _context.SaveChangesAsync();
            }
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.AvailableTables = await _context.Tables
                .Where(t => t.Status == "Empty")
                .OrderBy(t => t.TableName)
                .ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Booking booking, Dictionary<int, int> SelectedItems)
        {
            // 1. Kiểm tra trạng thái Model
            if (ModelState.IsValid)
            {
                try
                {
                    // Sinh mã Check-in tự động
                    string ran = new Random().Next(1000, 9999).ToString();
                    booking.CheckInCode = "BK" + DateTime.Now.ToString("ddMM") + ran;
                    booking.CreatedAt = DateTime.Now;
                    booking.Status = "Confirmed";

                    // Xử lý trạng thái bàn nếu Admin chọn bàn ngay khi tạo
                    if (booking.TableId != null && booking.TableId > 0)
                    {
                        var table = await _context.Tables.FindAsync(booking.TableId);
                        if (table != null)
                        {
                            table.Status = "Reserved";
                            _context.Tables.Update(table);
                        }
                    }
                    else
                    {
                        booking.TableId = null; // Đảm bảo null nếu không chọn
                    }

                    // Lưu thông tin Booking vào DB
                    _context.Bookings.Add(booking);
                    await _context.SaveChangesAsync();

                    // 2. Lưu danh sách món ăn đã đặt trước (PreOrderItems)
                    if (SelectedItems != null && SelectedItems.Any())
                    {
                        foreach (var item in SelectedItems.Where(x => x.Value > 0))
                        {
                            _context.PreOrderItems.Add(new PreOrderItem
                            {
                                BookingId = booking.BookingId,
                                ProductId = item.Key,
                                Quantity = item.Value
                            });
                        }
                        await _context.SaveChangesAsync();
                    }

                    TempData["Success"] = $"Đã tạo đơn đặt bàn thành công cho {booking.FullName}. Mã: {booking.CheckInCode}";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Có lỗi xảy ra khi lưu đơn hàng: " + ex.Message);
                }
            }

            // 3. Nếu Model không hợp lệ hoặc có lỗi, Load lại danh sách bàn để hiển thị lại View
            ViewBag.AvailableTables = await _context.Tables
                .Where(t => t.Status == "Empty")
                .OrderBy(t => t.TableName)
                .ToListAsync();

            // Cần load thêm danh sách món ăn (Products) để View có thể render lại form chọn món
            ViewBag.Products = await _context.Products.ToListAsync();

            return View(booking);
        }
    }
    }