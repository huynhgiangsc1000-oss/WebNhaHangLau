using DoAnCoSo.Data;
using DoAnCoSo.Models;
using DoAnCoSo.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Security.Claims;

namespace DoAnCoSo.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class BookingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly EmailSender _emailSender; // Thêm dòng này

        // Cập nhật Constructor để nhận EmailSender
        public BookingController(ApplicationDbContext context, UserManager<User> userManager, EmailSender emailSender)
        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender; // Thêm dòng này
        }

        // Hiển thị trang đặt bàn
        public IActionResult Index()
        {
            return View();
        }

        // Xử lý gửi form đặt bàn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Booking booking)
        {
            if (ModelState.IsValid)
            {
                var userIdStr = _userManager.GetUserId(User);
                if (!string.IsNullOrEmpty(userIdStr))
                {
                    booking.UserId = int.Parse(userIdStr);
                }

                // Tạo mã Check-in
                string randomCode = "NH-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
                booking.CheckInCode = randomCode;
                booking.Status = "Pending";
                booking.CreatedAt = DateTime.Now;

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                // --- GỬI MAIL VỚI QUICKCHART API ---
                try
                {
                    // Sử dụng QuickChart API: Ổn định, sắc nét và không bị chặn bởi Gmail
                    string qrCodeImageUrl = $"https://quickchart.io/qr?text={randomCode}&size=200&ecLevel=Q";

                    string subject = $"XÁC NHẬN ĐẶT BÀN - MÃ: {randomCode}";
                    string htmlBody = $@"
            <div style='font-family: Arial, sans-serif; border: 1px solid #f0f0f0; padding: 25px; max-width: 500px; margin: auto; border-radius: 15px;'>
                <h2 style='color: #dc3545; text-align: center;'>ĐẶT BÀN THÀNH CÔNG!</h2>
                <p>Chào <b>{booking.FullName}</b>,</p>
                <p>Yêu cầu đặt bàn của bạn đã được ghi nhận.</p>
                
                <div style='background-color: #fdf2f2; padding: 20px; border-radius: 10px; text-align: center; border: 1px dashed #dc3545;'>
                    <p style='font-weight: bold;'>Mã QR Check-in của bạn:</p>
                    <img src='{qrCodeImageUrl}' width='180' height='180' style='display: block; margin: 0 auto;' alt='QR Code' />
                    <h3 style='color: #007bff; letter-spacing: 2px;'>{randomCode}</h3>
                </div>

                <div style='margin-top: 20px; padding: 15px; border: 1px solid #eee; border-radius: 8px;'>
                    <p style='color: #dc3545; font-weight: bold; margin-bottom: 10px;'>Thông tin chi tiết:</p>
                    <p>📅 Ngày: <b>{booking.BookingDate:dd/MM/yyyy}</b></p>
                    <p>⏰ Giờ: <b>{booking.BookingDate:HH:mm}</b></p>
                    <p>👥 Khách: <b>{booking.GuestCount} người</b></p>
                </div>
            </div>";

                    var userEmail = booking.Email ?? (await _userManager.GetUserAsync(User))?.Email;
                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        await _emailSender.SendEmailAsync(userEmail, subject, htmlBody);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Email Error]: {ex.Message}");
                }

                TempData["Success"] = $"Mã của bạn là: {booking.CheckInCode}. Check email để nhận QR!";
                return RedirectToAction(nameof(Index));
            }
            return View("Index", booking);
        }
        public async Task<IActionResult> History()
        {
            // Lấy UserId của người dùng hiện tại[cite: 10, 13]
            var userIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userIdStr))
            {
                return Challenge(); // Yêu cầu đăng nhập nếu chưa[cite: 13]
            }

            int userId = int.Parse(userIdStr);

            // Lấy danh sách đặt bàn, kèm theo thông tin bàn nếu đã được Admin gán[cite: 14]
            var bookings = await _context.Bookings
                .Include(b => b.Table)
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();

            return View(bookings);
        }
    }
}