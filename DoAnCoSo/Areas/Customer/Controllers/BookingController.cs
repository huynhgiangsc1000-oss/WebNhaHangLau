using DoAnCoSo.Data;
using DoAnCoSo.Models;
using DoAnCoSo.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DoAnCoSo.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class BookingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly EmailSender _emailSender;

        public BookingController(ApplicationDbContext context, UserManager<User> userManager, EmailSender emailSender)
        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
        }

        public async Task<IActionResult> Index()
        {
            // Nạp danh sách sản phẩm để hiển thị trên form đặt bàn
            ViewBag.Products = await _context.Products.Where(p => p.IsAvailable).ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Booking booking, Dictionary<int, int> SelectedItems)
        {
            if (ModelState.IsValid)
            {
                var userIdStr = _userManager.GetUserId(User);
                if (!string.IsNullOrEmpty(userIdStr)) booking.UserId = int.Parse(userIdStr);

                string randomCode = "NH-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
                booking.CheckInCode = randomCode;
                booking.Status = "Pending";
                booking.CreatedAt = DateTime.Now;

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync(); // Lưu để nhận BookingId

                // Lưu các món khách chọn vào PreOrderItems
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
                    await _context.SaveChangesAsync();
                }

                // Gửi email xác nhận
                try
                {
                    var domain = $"{Request.Scheme}://{Request.Host}";
                    string accessUrl = $"{domain}/Customer/Table/AccessTable?checkInCode={randomCode}";
                    string qrCodeImageUrl = $"https://quickchart.io/qr?text={Uri.EscapeDataString(accessUrl)}&size=200&ecLevel=Q";

                    string subject = $"XÁC NHẬN ĐẶT BÀN - MÃ: {randomCode}";
                    string htmlBody = $@"
                        <div style='font-family: Arial, sans-serif; border: 1px solid #f0f0f0; padding: 25px; max-width: 500px; margin: auto; border-radius: 15px;'>
                            <h2 style='color: #dc3545; text-align: center;'>ĐẶT BÀN THÀNH CÔNG!</h2>
                            <p>Chào <b>{booking.FullName}</b>,</p>
                            <p>Vui lòng đưa mã này cho nhân viên hoặc <b>quét mã QR tại bàn</b> để bắt đầu gọi món.</p>
                            
                            <div style='background-color: #fdf2f2; padding: 20px; border-radius: 10px; text-align: center; border: 1px dashed #dc3545;'>
                                <p style='font-weight: bold;'>Quét mã này khi bạn đã ngồi vào bàn:</p>
                                <a href='{accessUrl}'><img src='{qrCodeImageUrl}' width='180' height='180' style='display: block; margin: 0 auto;' alt='QR Code' /></a>
                                <h3 style='color: #007bff; letter-spacing: 2px;'>{randomCode}</h3>
                            </div>

                            <div style='margin-top: 20px; padding: 15px; border: 1px solid #eee; border-radius: 8px;'>
                                <p style='color: #dc3545; font-weight: bold; margin-bottom: 10px;'>Thông tin lịch hẹn:</p>
                                <p>📅 Ngày: <b>{booking.BookingDate:dd/MM/yyyy}</b></p>
                                <p>⏰ Giờ: <b>{booking.BookingDate:HH:mm}</b></p>
                            </div>
                        </div>";

                    var userEmail = booking.Email ?? (await _userManager.GetUserAsync(User))?.Email;
                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        await _emailSender.SendEmailAsync(userEmail, subject, htmlBody);
                    }
                }
                catch (Exception ex) { Console.WriteLine($"[Email Error]: {ex.Message}"); }

                TempData["Success"] = $"Đặt bàn thành công! Check email để nhận QR truy cập bàn.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Products = await _context.Products.Where(p => p.IsAvailable).ToListAsync();
            return View("Index", booking);
        }

        public async Task<IActionResult> History(string search, string status)
        {
            var userIdStr = _userManager.GetUserId(User);
            var query = _context.Bookings
                .Include(b => b.Table)
                .Where(b => b.UserId == int.Parse(userIdStr))
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(b => b.CheckInCode.Contains(search));

            if (!string.IsNullOrEmpty(status))
                query = query.Where(b => b.Status == status);

            var bookings = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
            return View(bookings);
        }
    }
}