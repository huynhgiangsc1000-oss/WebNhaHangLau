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
            LoadViewBagSync(); // Dùng đồng bộ để tránh lỗi Async/SQL
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
                // 1. Tính toán số tiền (Dùng ToList() để tránh lệnh WITH của SQL Server cũ)
                decimal rawTotal = 0;
                if (SelectedItems != null && SelectedItems.Any(x => x.Value > 0))
                {
                    var productIds = SelectedItems.Keys.ToList();
                    var products = _context.Products.Where(p => productIds.Contains(p.ProductId)).ToList();

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
                _context.SaveChanges(); // Lưu booking trước để lấy BookingId

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

                // 3. Gửi Email (Vẫn giữ async vì không liên quan tới DB)
                await SendBookingConfirmationEmail(booking);

                TempData["Success"] = "Đặt bàn thành công! Mã xác nhận: " + booking.CheckInCode;
                return RedirectToAction(nameof(Index));
            }

            LoadViewBagSync();
            return View("Index", booking);
        }

        private void LoadViewBagSync()
        {
            // Thay vì dùng await ... ToListAsync(), dùng .ToList() để tránh lỗi câu lệnh SQL phức tạp
            ViewBag.Products = _context.Products.Where(p => p.IsAvailable).OrderBy(p => p.ProductName).ToList();
            ViewBag.Categories = _context.Categories.ToList();
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
                    <p>Mã: <strong>{booking.CheckInCode}</strong></p>
                    <img src='{qrImageUrl}' />
                </div>";

                await _emailSender.SendEmailAsync(booking.Email, "Xác nhận đặt bàn", htmlMessage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }
    }
}