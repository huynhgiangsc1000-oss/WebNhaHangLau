using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnCoSo.Data;
using DoAnCoSo.Models;
using System.Text.Encodings.Web;

namespace DoAnCoSo.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class BookingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IEmailSender _emailSender;

        public BookingController(ApplicationDbContext context, UserManager<User> userManager, IEmailSender emailSender)
        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Booking booking, Dictionary<int, int> SelectedItems)
        {
            ModelState.Remove(nameof(Booking.User));
            ModelState.Remove(nameof(Booking.Table));

            if (ModelState.IsValid)
            {
                decimal rawTotal = 0;

                // 1. Dùng Raw SQL để lấy danh sách sản phẩm (Tránh lỗi SQL 'WITH')
                if (SelectedItems != null && SelectedItems.Any(x => x.Value > 0))
                {
                    var productIds = SelectedItems.Keys.ToList();
                    string idList = string.Join(",", productIds);

                    var products = _context.Products
                        .FromSqlRaw($"SELECT * FROM Products WHERE ProductId IN ({idList})")
                        .ToList();

                    foreach (var item in SelectedItems.Where(x => x.Value > 0))
                    {
                        var product = products.FirstOrDefault(p => p.ProductId == item.Key);
                        if (product != null) rawTotal += (product.Price * item.Value);
                    }
                }

                booking.TotalAmount = rawTotal;
                booking.CheckInCode = "NH-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
                booking.Status = "Pending";
                booking.CreatedAt = DateTime.Now;

                var user = await _userManager.GetUserAsync(User);
                if (user != null) booking.UserId = user.Id;

                _context.Bookings.Add(booking);
                _context.SaveChanges();

                // 2. Lưu chi tiết các món
                if (SelectedItems != null)
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
                    _context.SaveChanges();
                }

                // 3. Gửi Email thông báo
                await SendBookingConfirmationEmail(booking);

                TempData["Success"] = "Đặt bàn thành công! Mã xác nhận: " + booking.CheckInCode;
                return RedirectToAction(nameof(Index));
            }

            LoadViewBagSync();
            return View("Index", booking);
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
                decimal depositAmount = booking.TotalAmount * 0.3m;
                string qrData = $"CheckIn:{booking.CheckInCode}|Amount:{depositAmount}";
                string qrImageUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=200x200&data={UrlEncoder.Default.Encode(qrData)}";

                string htmlMessage = $@"<div style='font-family: Arial; padding: 20px;'>
                    <h2>XÁC NHẬN ĐẶT BÀN</h2>
                    <p>Chào <strong>{booking.FullName}</strong>, đơn đặt bàn của bạn đã được ghi nhận.</p>
                    <div style='background: #f8f9fa; padding: 15px; border-left: 5px solid #dc3545;'>
                        <p><strong>Mã đặt bàn:</strong> {booking.CheckInCode}</p>
                        <p><strong>Tổng hóa đơn:</strong> {booking.TotalAmount:N0} VNĐ</p>
                        <p><strong>Số tiền cần cọc (30%):</strong> {depositAmount:N0} VNĐ</p>
                        <img src='{qrImageUrl}' />
                    </div>
                    <p>Vui lòng xuất trình mã <strong>{booking.CheckInCode}</strong> khi đến nhà hàng.</p>
                </div>";

                await _emailSender.SendEmailAsync(booking.Email, "Xác nhận đặt bàn", htmlMessage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi gửi email: {ex.Message}");
            }
        }
    }
}