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

            if (vnpay.ValidateSignature(vnp_SecureHash, vnp_HashSecret) && vnp_ResponseCode == "00")
            {
                int orderId = int.Parse(txnRef);
                await CompleteOrderProcess(orderId, "VNPAY");
                return RedirectToAction("PaymentSuccess", new { id = orderId });
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
            var order = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null) return false;

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // A. Cập nhật trạng thái đơn hàng
                    order.Status = "Completed";
                    order.PaymentMethod = paymentMethod;

                    // B. Giải phóng bàn vật lý (Chuyển sang Xanh)
                    if (order.Table != null)
                    {
                        order.Table.Status = "Empty";
                    }

                    // C. ĐÓNG PHIÊN ĐẶT BÀN (Rất quan trọng để chặn đặt món tiếp)
                    var activeBooking = await _context.Bookings
                        .FirstOrDefaultAsync(b => b.TableId == order.TableId
                                             && b.UserId == order.UserId
                                             && b.Status == "CheckedIn");
                    if (activeBooking != null)
                    {
                        activeBooking.Status = "Completed";
                    }

                    // D. Xử lý điểm thưởng & Hạng thành viên
                    if (order.User != null)
                    {
                        order.User.Points += (int)(order.TotalAmount / 10000); // 10k = 1đ
                        // Cập nhật hạng ở đây nếu bạn đã có hàm UpdateUserRank
                    }

                    // E. DỌN DẸP GIỎ HÀNG (Xóa các món khách đã đặt hoặc định đặt)
                    var cartItems = _context.CartItems.Where(c => c.UserId == order.UserId);
                    _context.CartItems.RemoveRange(cartItems);

                    // F. Xóa Session máy khách để ép kết thúc phiên làm việc
                    HttpContext.Session.Remove("TableId");
                    Response.Cookies.Delete("SavedTableId");

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch
                {
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