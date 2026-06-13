using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnCoSo.Data;
using DoAnCoSo.Models;
using DoAnCoSo.Models.Libraries;
using System.Text.Encodings.Web;

namespace DoAnCoSo.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class BookingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _configuration;

        public BookingController(ApplicationDbContext context, UserManager<User> userManager, IEmailSender emailSender, IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            LoadViewBagSync();
            var booking = new Booking
            {
                BookingDate = DateTime.Now.AddHours(2),
                GuestCount = 1
            };
            return View(booking);
        }

        [HttpGet]
        public IActionResult GetAvailableTables(DateTime bookingDate, int guestCount)
        {
            // Lấy danh sách bàn đã được đặt trong khoảng +/- 1 tiếng
            var twoHoursBefore = bookingDate.AddHours(-1);
            var twoHoursAfter = bookingDate.AddHours(1);

            var bookedTableIds = _context.Bookings
                .Where(b => (b.Status == "Pending" || b.Status == "Confirmed" || b.Status == "CheckedIn") && b.BookingDate >= twoHoursBefore && b.BookingDate <= twoHoursAfter && b.TableId != null)
                .Select(b => b.TableId)
                .ToList();

            var availableTables = _context.Tables
                .Where(t => t.Capacity >= guestCount && !bookedTableIds.Contains(t.TableId))
                .Select(t => new { t.TableId, t.TableName, t.Capacity })
                .ToList();

            return Json(availableTables);
        }

        public class CartItemDto
        {
            public int id { get; set; }
            public int qty { get; set; }
            public decimal price { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> CheckDiscountCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return Json(new { success = false, message = "Vui lòng nhập mã giảm giá" });

            var promo = await _context.Promotions.FirstOrDefaultAsync(p => p.CouponCode == code);
            
            if (promo == null) return Json(new { success = false, message = "Mã giảm giá không tồn tại" });
            if (!promo.IsActive) return Json(new { success = false, message = "Mã giảm giá đã ngừng hoạt động" });
            if (promo.StartDate > DateTime.Now) return Json(new { success = false, message = "Mã giảm giá chưa đến thời gian áp dụng" });
            if (promo.EndDate < DateTime.Now) return Json(new { success = false, message = "Mã giảm giá đã hết hạn" });

            return Json(new { success = true, discount = promo.DiscountValue, message = $"Áp dụng thành công! Giảm {promo.DiscountValue}%" });
        }

        [HttpGet]
        public async Task<IActionResult> GetActivePromotions()
        {
            var now = DateTime.Now;
            var promotions = await _context.Promotions
                .Where(p => p.IsActive && p.StartDate <= now && p.EndDate >= now)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.CouponCode,
                    p.DiscountValue,
                    p.Description,
                    EndDate = p.EndDate.ToString("dd/MM/yyyy")
                })
                .ToListAsync();

            return Json(promotions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Booking booking, string CartDetails)
        {
            ModelState.Remove(nameof(Booking.User));
            ModelState.Remove(nameof(Booking.Table));
            ModelState.Remove(nameof(Booking.PreOrderItems)); // Bỏ qua validate danh sách món

            if (ModelState.IsValid)
            {
                decimal rawTotal = 0;
                List<CartItemDto> cartItems = new List<CartItemDto>();

                // 1. Phân tích giỏ hàng từ JSON
                if (!string.IsNullOrEmpty(CartDetails))
                {
                    try
                    {
                        cartItems = System.Text.Json.JsonSerializer.Deserialize<List<CartItemDto>>(CartDetails) ?? new List<CartItemDto>();
                    }
                    catch (Exception) { /* Bỏ qua lỗi parse */ }

                    if (cartItems.Any())
                    {
                        var productIds = cartItems.Select(x => x.id).ToList();
                        string idList = string.Join(",", productIds);

                        var products = _context.Products
                            .FromSqlRaw($"SELECT * FROM Products WHERE ProductId IN ({idList})")
                            .ToList();

                        foreach (var item in cartItems)
                        {
                            var product = products.FirstOrDefault(p => p.ProductId == item.id);
                            if (product != null) rawTotal += (product.Price * item.qty);
                        }
                    }
                }

                // Tính toán giảm giá nếu có mã giảm giá
                decimal finalTotal = rawTotal;
                if (!string.IsNullOrEmpty(booking.DiscountCode))
                {
                    var promo = await _context.Promotions.FirstOrDefaultAsync(p => p.CouponCode == booking.DiscountCode && p.IsActive);
                    if (promo != null && promo.StartDate <= DateTime.Now && promo.EndDate >= DateTime.Now)
                    {
                        finalTotal = rawTotal * (1 - promo.DiscountValue / 100m);
                    }
                }

                booking.TotalAmount = finalTotal;
                booking.CheckInCode = "NH-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
                booking.Status = "Pending";
                booking.CreatedAt = DateTime.Now;

                var user = await _userManager.GetUserAsync(User);
                if (user != null) booking.UserId = user.Id;

                _context.Bookings.Add(booking);
                _context.SaveChanges();

                // 2. Lưu chi tiết các món
                if (cartItems.Any())
                {
                    foreach (var item in cartItems)
                    {
                        _context.PreOrderItems.Add(new PreOrderItem
                        {
                            BookingId = booking.BookingId,
                            ProductId = item.id,
                            Quantity = item.qty
                        });
                    }
                    _context.SaveChanges();
                }

                // Bỏ gọi SendBookingConfirmationEmail ở đây
                // Chuyển hướng sang trang thanh toán cọc
                return RedirectToAction(nameof(DepositPayment), new { id = booking.BookingId });
            }

            LoadViewBagSync();
            return View("Index", booking);
        }

        [HttpGet]
        public async Task<IActionResult> DepositPayment(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.PreOrderItems).ThenInclude(p => p.Product)
                .FirstOrDefaultAsync(b => b.BookingId == id);
                
            if (booking == null) return NotFound();

            // Nếu không có món chọn trước thì không cần cọc, confirm luôn
            if (!booking.PreOrderItems.Any() || booking.TotalAmount <= 0)
            {
                return RedirectToAction(nameof(ConfirmDepositDirect), new { id = booking.BookingId });
            }

            return View(booking);
        }

        public async Task<IActionResult> ConfirmDepositDirect(int id)
        {
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == id);
            if (booking == null) return NotFound();

            await SendBookingConfirmationEmail(booking);
            TempData["Success"] = "Đặt bàn thành công! Mã xác nhận: " + booking.CheckInCode + " đã được gửi về Email.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayDepositVnpay(int id)
        {
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == id);
            if (booking == null) return NotFound();

            decimal depositAmount = booking.TotalAmount * 0.3m;
            if (depositAmount < 30000) depositAmount = 30000;

            string vnp_Returnurl = Url.Action("DepositCallback", "Booking", new { area = "Customer" }, Request.Scheme);
            string vnp_Url = _configuration["Vnpay:BaseUrl"];
            string vnp_TmnCode = _configuration["Vnpay:TmnCode"];
            string vnp_HashSecret = _configuration["Vnpay:HashSecret"];

            VnPayLibrary vnpay = new VnPayLibrary();
            vnpay.AddRequestData("vnp_Version", "2.1.0");
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
            vnpay.AddRequestData("vnp_Amount", ((long)(depositAmount * 100)).ToString());
            vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", "VND");
            vnpay.AddRequestData("vnp_IpAddr", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");
            vnpay.AddRequestData("vnp_Locale", "vn");
            vnpay.AddRequestData("vnp_OrderInfo", "Thanh toan tien coc ban #" + booking.CheckInCode);
            vnpay.AddRequestData("vnp_OrderType", "other");
            vnpay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);
            vnpay.AddRequestData("vnp_TxnRef", booking.BookingId.ToString());

            string paymentUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
            return Redirect(paymentUrl);
        }

        public async Task<IActionResult> DepositCallback()
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
                int bookingId = int.Parse(txnRef);
                var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == bookingId);
                
                if (booking != null)
                {
                    // Lưu tiền cọc đã thanh toán
                    decimal depositAmount = booking.TotalAmount * 0.3m;
                    if (depositAmount < 30000) depositAmount = 30000;
                    booking.DepositAmount = depositAmount;
                    await _context.SaveChangesAsync();

                    await SendBookingConfirmationEmail(booking);

                    TempData["Success"] = "Thanh toán cọc thành công! Mã Check-in: " + booking.CheckInCode + " đã được gửi về Email.";
                    return RedirectToAction(nameof(Index));
                }
            }

            TempData["Error"] = "Thanh toán thất bại hoặc đã bị hủy.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        public async Task<IActionResult> History(string search, string status)
        {
            var userIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userIdStr)) return Challenge();
            int userId = int.Parse(userIdStr);

            var query = _context.Bookings
                .Include(b => b.Table)
                .Where(b => b.UserId == userId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(b => b.CheckInCode.Contains(search));
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(b => b.Status == status);
            }

            var bookings = await query.OrderByDescending(b => b.BookingDate).ToListAsync();
            return View(bookings);
        }

        private void LoadViewBagSync()
        {
            // Dùng Raw SQL để lấy danh mục và sản phẩm, tránh EF sinh lệnh WITH
            ViewBag.Products = _context.Products
                .FromSqlRaw("SELECT * FROM Products WHERE IsAvailable = 1 ORDER BY ProductName")
                .ToList();

            ViewBag.Categories = _context.Categories
                .FromSqlRaw("SELECT * FROM Categories")
                .ToList();
        }

        private async Task SendBookingConfirmationEmail(Booking booking)
        {
            try
            {
                string subject = $"XÁC NHẬN ĐẶT BÀN THÀNH CÔNG - MÃ: {booking.CheckInCode}";
                string checkInQrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=250x250&data={booking.CheckInCode}";

                string htmlMessage = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #eaeaea; border-radius: 8px; padding: 20px;'>
                    <h2 style='color: #dc3545; text-align: center; margin-bottom: 20px; font-size: 18px;'>ĐẶT BÀN THÀNH CÔNG!</h2>
                    <p><strong>Chào {booking.FullName},</strong></p>
                    <p style='color: #555; line-height: 1.5;'>Vui lòng đưa mã này cho nhân viên hoặc quét mã QR tại bàn để bắt đầu gọi món.</p>
                    
                    <div style='text-align: center; margin-top: 20px; border: 1px dashed #dc3545; padding: 20px; border-radius: 8px; background-color: #fffafb;'>
                        <p style='margin-top: 0; color: #dc3545; font-weight: bold;'>Quét mã này khi bạn đã ngồi vào bàn:</p>
                        <img src='{checkInQrUrl}' alt='QR Code' style='max-width: 180px;' />
                        <h3 style='color: #0056b3; margin: 15px 0 0 0; letter-spacing: 2px;'>{booking.CheckInCode}</h3>
                    </div>

                    <div style='margin-top: 25px; border-top: 1px solid #eee; padding-top: 15px;'>
                        <p style='color: #dc3545; font-weight: bold; margin-bottom: 10px;'>Thông tin lịch hẹn:</p>
                        <p style='margin: 5px 0; color: #555;'>📅 Ngày: <strong>{booking.BookingDate:dd/MM/yyyy}</strong></p>
                        <p style='margin: 5px 0; color: #555;'>⏰ Giờ: <strong>{booking.BookingDate:HH:mm}</strong></p>
                    </div>
                </div>";

                await _emailSender.SendEmailAsync(booking.Email, subject, htmlMessage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi gửi email: {ex.Message}");
            }
        }
    }
}