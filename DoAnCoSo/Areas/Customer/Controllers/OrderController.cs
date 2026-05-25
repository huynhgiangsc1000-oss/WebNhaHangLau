using DoAnCoSo.Data;
using DoAnCoSo.Models.Libraries;
using DoAnCoSo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnCoSo.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IConfiguration _configuration;

        public OrderController(ApplicationDbContext context, UserManager<User> userManager, IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
        }

        // 1. XEM CHI TIẾT ĐƠN HÀNG
        public async Task<IActionResult> OrderDetails(int id)
        {
            var userIdString = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userIdString)) return Challenge();
            int userId = int.Parse(userIdString);

            var order = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.Promotion)
                .Include(o => o.User).ThenInclude(u => u.Rank)
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(m => m.OrderId == id);

            if (order == null || order.UserId != userId) return NotFound();
            return View(order);
        }

        // 2. LỊCH SỬ ĐƠN HÀNG
        public async Task<IActionResult> History(bool? success)
        {
            var userIdString = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userIdString)) return Challenge();
            int userId = int.Parse(userIdString);

            var orders = await _context.Orders
                .Include(o => o.Table)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            ViewBag.Success = success;
            return View(orders);
        }

        // 3. THANH TOÁN VNPAY
        public async Task<IActionResult> PayVnpay(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Promotion)
                .Include(o => o.User).ThenInclude(u => u.Rank)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null) return NotFound();

            // Tính toán giảm giá (lấy mức cao nhất giữa Rank và Voucher)
            decimal rankDiscount = order.User?.Rank?.DiscountPercent ?? 0;
            decimal promoDiscount = order.Promotion?.DiscountValue ?? 0;
            decimal finalDiscountPercent = Math.Max(rankDiscount, promoDiscount);
            decimal finalAmount = order.TotalAmount * (1 - (finalDiscountPercent / 100m));

            // Cấu hình VNPAY
            string vnp_Returnurl = _configuration["Vnpay:PaymentBackUrl"];
            string vnp_Url = _configuration["Vnpay:BaseUrl"];
            string vnp_TmnCode = _configuration["Vnpay:TmnCode"];
            string vnp_HashSecret = _configuration["Vnpay:HashSecret"];

            VnPayLibrary vnpay = new VnPayLibrary();
            vnpay.AddRequestData("vnp_Version", "2.1.0");
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
            vnpay.AddRequestData("vnp_Amount", ((long)(finalAmount * 100)).ToString());
            vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", "VND");
            vnpay.AddRequestData("vnp_IpAddr", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");
            vnpay.AddRequestData("vnp_Locale", "vn");
            vnpay.AddRequestData("vnp_OrderInfo", "Thanh toan don hang #" + order.OrderId);
            vnpay.AddRequestData("vnp_OrderType", "other");
            vnpay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);
            vnpay.AddRequestData("vnp_TxnRef", order.OrderId.ToString());

            string paymentUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
            return Redirect(paymentUrl);
        }

        // 4. CALLBACK SAU KHI THANH TOÁN VNPAY XONG
        // 4. CALLBACK SAU KHI THANH TOÁN VNPAY XONG
        public async Task<IActionResult> PaymentCallback()
        {
            var response = Request.Query;
            string vnp_HashSecret = _configuration["Vnpay:HashSecret"];
            VnPayLibrary vnpay = new VnPayLibrary();

            foreach (var (key, value) in response)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                    vnpay.AddResponseData(key, value);
            }

            string txnRef = vnpay.GetResponseData("vnp_TxnRef");
            string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
            string vnp_SecureHash = response["vnp_SecureHash"];

            // Kiểm tra chữ ký và trạng thái thành công ("00" là thành công)
            if (vnpay.ValidateSignature(vnp_SecureHash, vnp_HashSecret) && vnp_ResponseCode == "00")
            {
                int orderId = int.Parse(txnRef);

                // Gọi hàm dùng chung để chốt đơn và giải phóng bàn trong DB
                bool success = await CompleteOrderProcess(orderId, "VNPAY");

                if (success)
                {
                    // --- XÓA SESSION VÀ COOKIE Ở ĐÂY ---
                    // Việc này giúp khách hàng không còn "giữ" bàn trên trình duyệt
                    HttpContext.Session.Remove("TableId");
                    Response.Cookies.Delete("SavedTableId");

                    return RedirectToAction("PaymentSuccess", new { id = orderId });
                }
            }

            return RedirectToAction("OrderDetails", new { id = int.Parse(txnRef), error = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestCashPayment(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Table)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });

            // BƯỚC QUAN TRỌNG:
            // 1. Chỉ cập nhật trạng thái đơn hàng thành "AwaitingPayment" (Chờ thanh toán)
            // 2. KHÔNG set order.Table.Status = "Empty" -> Giữ bàn đỏ trên sơ đồ Admin
            order.Status = "AwaitingPayment";
            order.PaymentMethod = "Cash";

            // Xóa session/cookie bàn trên máy khách để họ không đặt thêm món được nữa
            HttpContext.Session.Remove("TableId");
            Response.Cookies.Delete("SavedTableId");

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Yêu cầu đã được gửi! Vui lòng tới quầy thu ngân để hoàn tất thủ tục."
            });
        }

        // 6. LOGIC DÙNG CHUNG: HOÀN TẤT ĐƠN HÀNG & GIẢI PHÓNG PHIÊN
        private async Task<bool> CompleteOrderProcess(int orderId, string paymentMethod)
        {
            // 1. Lấy thông tin đơn hàng cùng các quan hệ cần thiết
            var order = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.User).ThenInclude(u => u.Rank)
                .Include(o => o.Promotion)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null) return false;

            // 2. Tính toán giảm giá (Ưu tiên mức cao nhất)
            decimal rankDiscountPercent = order.User?.Rank?.DiscountPercent ?? 0;
            decimal promoDiscountPercent = (order.Promotion != null) ? order.Promotion.DiscountValue : 0;

            decimal finalDiscountPercent = Math.Max(rankDiscountPercent, promoDiscountPercent);
            decimal discountValue = order.TotalAmount * (finalDiscountPercent / 100m);

            // 3. Sử dụng Transaction để đảm bảo tính toàn vẹn dữ liệu (Data Integrity)
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Cập nhật thông tin đơn hàng
                    order.Status = "Completed";
                    order.PaymentMethod = paymentMethod;
                    order.DiscountAmount = discountValue; // Lưu số tiền được giảm vào DB

                    // Cập nhật điểm thưởng
                    if (order.User != null)
                    {
                        order.User.Points += (int)(order.TotalAmount / 10000);
                    }

                    // Giải phóng bàn: Chuyển trạng thái bàn về "Empty" (Trống)
                    if (order.Table != null)
                    {
                        order.Table.Status = "Empty";
                    }

                    // Lưu tất cả thay đổi
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return true;
                }
                catch (Exception)
                {
                    // Nếu xảy ra lỗi, hủy bỏ tất cả thao tác trong transaction
                    await transaction.RollbackAsync();
                    return false;
                }
            }
        }
        // 7. TRANG THÔNG BÁO THÀNH CÔNG
        public async Task<IActionResult> PaymentSuccess(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                .Include(o => o.Table)
                .Include(o => o.Promotion)
                .Include(o => o.User).ThenInclude(u => u.Rank)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null) return NotFound();
            return View(order);
        }

        // 8. ÁP DỤNG MÃ GIẢM GIÁ
        [HttpPost]
        // Trong OrderController.cs
        [HttpPost]
        public async Task<IActionResult> ApplyPromotion(int orderId, string couponCode)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return Json(new { success = false, message = "Không tìm thấy đơn hàng" });

            if (order.Status == "Completed" || order.Status == "Cancelled")
                return Json(new { success = false, message = "Đơn hàng đã chốt, không thể áp mã!" });

            // Lấy thời gian hiện tại
            var now = DateTime.Now;

            // SỬA TẠI ĐÂY: Thêm kiểm tra StartDate và EndDate
            var promotion = await _context.Promotions
                .FirstOrDefaultAsync(p => p.CouponCode == couponCode
                                          && p.IsActive
                                          && p.StartDate <= now
                                          && p.EndDate >= now); // Thời gian hiện tại phải nằm trong khoảng cho phép

            if (promotion == null)
            {
                // Kiểm tra xem lỗi do code sai hay do hết hạn để báo khách cho chuẩn (tùy chọn)
                var checkExists = await _context.Promotions.AnyAsync(p => p.CouponCode == couponCode);
                if (!checkExists)
                    return Json(new { success = false, message = "Mã giảm giá không tồn tại" });

                return Json(new { success = false, message = "Mã giảm giá đã hết hạn hoặc chưa đến thời gian sử dụng" });
            }

            order.PromotionId = promotion.Id;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Đã áp dụng mã {promotion.Title}" });
        }

    }
}