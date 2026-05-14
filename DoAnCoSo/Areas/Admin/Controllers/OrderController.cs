using DoAnCoSo.Data;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnCoSo.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Staff")]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        public OrderController(ApplicationDbContext context) => _context = context;

        // 1. HIỂN THỊ DANH SÁCH ĐƠN HÀNG
        public async Task<IActionResult> Index(string status, string searchQuery, DateTime? searchDate)
        {
            var query = _context.Orders
                .Include(o => o.Table)
                .Include(o => o.User).ThenInclude(u => u.Rank)
                .Include(o => o.Promotion)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status)) query = query.Where(o => o.Status == status);

            if (!string.IsNullOrEmpty(searchQuery))
            {
                if (int.TryParse(searchQuery.Replace("#", ""), out int orderId))
                    query = query.Where(o => o.OrderId == orderId);
                else
                    query = query.Where(o => o.Table.TableName.Contains(searchQuery));
            }

            if (searchDate.HasValue) query = query.Where(o => o.OrderDate.Date == searchDate.Value.Date);

            var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();

            ViewBag.CurrentStatus = status;
            ViewBag.CurrentSearch = searchQuery;
            ViewBag.CurrentDate = searchDate?.ToString("yyyy-MM-dd");

            return View(orders);
        }

        // 2. CẬP NHẬT TRẠNG THÁI (Logic quan trọng nhất)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string Status)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var order = await _context.Orders
                        .Include(o => o.Table)
                        .Include(o => o.User)
                        .FirstOrDefaultAsync(o => o.OrderId == id);

                    if (order == null) return Json(new { success = false, message = "Không tìm thấy đơn hàng" });

                    // Gán trạng thái mới
                    order.Status = Status;

                    // NẾU ADMIN XÁC NHẬN "COMPLETED" (ĐÃ THU TIỀN XONG)
                    if (Status == "Completed")
                    {
                        // 1. Giải phóng bàn vật lý (Chuyển xanh)
                        if (order.Table != null)
                        {
                            order.Table.Status = "Empty";
                        }

                        // 2. Đóng phiên đặt bàn (Booking)
                        var activeBooking = await _context.Bookings
                            .FirstOrDefaultAsync(b => b.TableId == order.TableId &&
                                                     b.UserId == order.UserId &&
                                                     b.Status == "CheckedIn");
                        if (activeBooking != null)
                        {
                            activeBooking.Status = "Completed";
                        }

                        // 3. Cộng điểm tích lũy (10k = 1 điểm)
                        if (order.User != null)
                        {
                            order.User.Points += (int)(order.TotalAmount / 10000);
                            await UpdateUserRank(order.User);
                        }
                    }

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
    }
}