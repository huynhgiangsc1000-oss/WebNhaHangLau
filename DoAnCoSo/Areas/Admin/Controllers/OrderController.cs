using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace DoAnCoSo.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Staff")]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        public OrderController(ApplicationDbContext context) => _context = context;

        // Controllers/OrderController.cs (hoặc Admin/BookingManager tương ứng)
        // 1. HIỂN THỊ DANH SÁCH ĐƠN HÀNG PHÂN THEO BUỔI
        public async Task<IActionResult> Index(string status, string searchQuery, string startDate, string endDate, int page = 1)
        {
            int pageSize = 15; // Thay đổi thành 15 đơn hàng mỗi trang theo ý bạn

            var query = _context.Orders
                .Include(o => o.Table)
                .Include(o => o.User).ThenInclude(u => u.Rank)
                .Include(o => o.Promotion)
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
                .AsQueryable();

            // --- Các đoạn Filter (giữ nguyên logic của bạn) ---
            if (!string.IsNullOrEmpty(status)) query = query.Where(o => o.Status == status);
            else query = query.Where(o => o.Status != "Completed" && o.Status != "Cancelled");

            if (!string.IsNullOrEmpty(searchQuery))
            {
                string term = searchQuery.Trim().Replace("#", "");
                if (int.TryParse(term, out int orderId)) query = query.Where(o => o.OrderId == orderId);
                else query = query.Where(o => o.Table != null && o.Table.TableName.Contains(searchQuery));
            }

            if (DateTime.TryParseExact(startDate, "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime start))
                query = query.Where(o => o.OrderDate.Date >= start.Date);
            if (DateTime.TryParseExact(endDate, "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime end))
                query = query.Where(o => o.OrderDate.Date <= end.Date);
            // --------------------------------------------------

            // 1. Lấy tổng số lượng đơn hàng để tính tổng trang
            var totalOrders = await query.CountAsync();

            // 2. Phân trang: Bỏ qua (page-1)*pageSize và lấy pageSize đơn hàng
            // Lưu ý: Phải OrderBy trước khi Skip/Take
            var pagedOrders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 3. Grouping trên tập dữ liệu đã được giới hạn (15 đơn)
            var groupedOrders = pagedOrders.GroupBy(o => o.OrderDate.Date)
                                           .OrderByDescending(g => g.Key);

            ViewBag.Status = status;
            ViewBag.SearchQuery = searchQuery;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalOrders / pageSize);

            return View(groupedOrders);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        
        public async Task<IActionResult> UpdateStatus(int id, string Status)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Thêm Include cả Promotion và User.Rank để có đủ dữ liệu tính toán
                    var order = await _context.Orders
                        .Include(o => o.Table)
                        .Include(o => o.User).ThenInclude(u => u.Rank)
                        .Include(o => o.Promotion)
                        .FirstOrDefaultAsync(o => o.OrderId == id);

                    if (order == null) return Json(new { success = false, message = "Không tìm thấy đơn hàng" });

                    // BIỆN PHÁP BẢO VỆ CONTROLLER... (Giữ nguyên code chặn trạng thái cũ)

                    // NẾU LÀ HOÀN TẤT ĐƠN HÀNG
                    if (Status == "Completed")
                    {
                        // 1. TÍNH TOÁN LẠI GIẢM GIÁ (Giống như logic ở Customer Controller)
                        decimal rankDiscountPercent = order.User?.Rank?.DiscountPercent ?? 0;
                        decimal promoDiscountPercent = (order.Promotion != null) ? order.Promotion.DiscountValue : 0;
                        decimal finalDiscountPercent = Math.Max(rankDiscountPercent, promoDiscountPercent);

                        order.DiscountAmount = order.TotalAmount * (finalDiscountPercent / 100m); // LƯU VÀO DB
                        order.PaymentMethod = order.PaymentMethod ?? "Cash"; // Nếu chưa có thì mặc định là Cash

                        // 2. Giải phóng bàn
                        if (order.Table != null) order.Table.Status = "Empty";

                        // 3. Cộng điểm
                        if (order.User != null)
                        {
                            order.User.Points += (int)(order.TotalAmount / 10000);
                            await UpdateUserRank(order.User);
                        }

                        // 4. Đóng phiên Booking
                        var activeBooking = await _context.Bookings
                            .FirstOrDefaultAsync(b => b.TableId == order.TableId && b.UserId == order.UserId && b.Status == "CheckedIn");
                        if (activeBooking != null) activeBooking.Status = "Completed";
                    }
                    else if (Status == "Cancelled")
                    {
                        if (order.Table != null) order.Table.Status = "Empty";
                    }

                    order.Status = Status;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return Json(new { success = true });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = ex.Message });
                }
            }
        }

        private async Task UpdateUserRank(User user)
        {
            var ranks = await _context.Set<Rank>().OrderByDescending(r => r.RequiredPoints).ToListAsync();
            foreach (var rank in ranks)
            {
                if (user.Points >= rank.RequiredPoints)
                {
                    user.RankId = rank.RankId;
                    break;
                }
            }
        }

        public async Task<IActionResult> GetDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                .Include(o => o.Table)
                .Include(o => o.User).ThenInclude(u => u.Rank)
                .Include(o => o.Promotion)
                .FirstOrDefaultAsync(o => o.OrderId == id);
            return PartialView("_OrderDetailModal", order);
        }

        public async Task<IActionResult> PrintInvoice(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.Promotion)
                .Include(o => o.User).ThenInclude(u => u.Rank)
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(m => m.OrderId == id);
            return View(order);
        }
        [HttpGet]
        public async Task<IActionResult> GetPendingCounts()
        {
            // Đếm số đơn hàng có trạng thái "Pending" hoặc "Chờ xác nhận" (tùy thuộc vào chuỗi bạn lưu trong DB)
            int pendingOrders = await _context.Orders.CountAsync(o => o.Status == "Pending" || o.Status == "Chờ xác nhận");

            // Đếm số lượt đặt bàn chưa xử lý (ví dụ: trạng thái "Pending" hoặc "Chờ duyệt")
            int pendingBookings = await _context.Bookings.CountAsync(b => b.Status == "Pending" || b.Status == "Chờ duyệt");

            return Json(new
            {
                orders = pendingOrders,
                bookings = pendingBookings
            });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            // 1. Tìm đơn hàng
            var order = await _context.Orders
                .Include(o => o.OrderDetails) // Nếu bạn muốn xóa cả chi tiết đơn hàng
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
            }

            // 2. KIỂM TRA ĐIỀU KIỆN ĐƯỢC PHÉP XÓA
            // Chỉ cho phép xóa khi trạng thái là "Pending" hoặc "Cancelled"
            // (Giả định trạng thái trống tương đương với "Pending" trong logic của bạn)
            bool isPending = string.IsNullOrEmpty(order.Status) || order.Status == "Pending";
            bool isCancelled = order.Status == "Cancelled";

            if (!isPending && !isCancelled)
            {
                return Json(new { success = false, message = "Chỉ có thể xóa các đơn hàng đang chờ hoặc đã hủy." });
            }

            try
            {
                // 3. Thực hiện xóa
                // Nếu có OrderDetails, EF Core cần xóa chúng trước hoặc cấu hình Cascade Delete trong Database
                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Đã xóa đơn hàng thành công." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }
    }
}