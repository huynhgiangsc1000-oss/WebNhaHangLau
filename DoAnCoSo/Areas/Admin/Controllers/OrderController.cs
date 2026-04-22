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

        // 1. HIỂN THỊ DANH SÁCH ĐƠN HÀNG (CÓ LỌC & TÌM KIẾM)
        public async Task<IActionResult> Index(string status, string searchTable)
        {
            var query = _context.Orders
                .Include(o => o.Table)
                .Include(o => o.User)
                .AsQueryable();

            // Lọc theo trạng thái
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.Status == status);
            }

            // Tìm theo tên bàn
            if (!string.IsNullOrEmpty(searchTable))
            {
                query = query.Where(o => o.Table.TableName.Contains(searchTable));
            }

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            ViewBag.CurrentStatus = status;
            ViewBag.CurrentSearch = searchTable;

            return View(orders);
        }

        // 2. LẤY CHI TIẾT ĐƠN HÀNG (DÙNG CHO MODAL)
        public async Task<IActionResult> GetDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                .Include(o => o.Table)
                .Include(o => o.User).ThenInclude(u => u.Rank)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null) return NotFound();
            return PartialView("_OrderDetailModal", order);
        }

        // 3. CẬP NHẬT TRẠNG THÁI & XỬ LÝ ĐIỂM THƯỞNG
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            // Lấy đơn hàng bao gồm thông tin Bàn và Khách hàng để xử lý logic thanh toán
            var order = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });

            // Nếu đơn hàng đã Paid hoặc Cancelled thì không cho đổi trạng thái nữa (tùy nghiệp vụ)
            if (order.Status == "Paid" || order.Status == "Cancelled")
                return Json(new { success = false, message = "Đơn hàng đã hoàn tất hoặc đã hủy, không thể thay đổi." });

            try
            {
                string oldStatus = order.Status;
                order.Status = status;

                // --- LOGIC KHI THANH TOÁN THÀNH CÔNG (PAID) ---
                if (status == "Paid")
                {
                    // 1. Giải phóng bàn
                    if (order.Table != null)
                    {
                        order.Table.Status = "Empty";
                    }

                    // 2. Cộng điểm tích lũy cho khách hàng (Nếu đơn hàng có gắn User)
                    if (order.UserId != null && order.User != null)
                    {
                        // Quy tắc: 10,000đ = 1 điểm (Bạn có thể sửa lại tỉ lệ này)
                        int earnedPoints = (int)(order.TotalAmount / 10000);
                        order.User.Points += earnedPoints;

                        // 3. Tự động kiểm tra và cập nhật hạng (Rank)
                        await UpdateUserRank(order.User);
                    }
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Cập nhật trạng thái thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // PHƯƠNG THỨC HỖ TRỢ: TỰ ĐỘNG CẬP NHẬT HẠNG THEO ĐIỂM
        private async Task UpdateUserRank(User user)
        {
            // Lấy danh sách tất cả các hạng, sắp xếp theo điểm yêu cầu giảm dần
            var ranks = await _context.Set<Rank>().OrderByDescending(r => r.RequiredPoints).ToListAsync();

            foreach (var rank in ranks)
            {
                if (user.Points >= rank.RequiredPoints)
                {
                    user.RankId = rank.RankId;
                    break; // Tìm được hạng cao nhất phù hợp thì dừng lại
                }
            }
        }

        // 4. XÓA ĐƠN HÀNG (CHỈ NÊN CHO XÓA KHI TRẠNG THÁI LÀ CANCELLED)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return Json(new { success = false, message = "Không tìm thấy đơn hàng" });

            if (order.Status != "Cancelled")
                return Json(new { success = false, message = "Chỉ có thể xóa đơn hàng đã hủy!" });

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}