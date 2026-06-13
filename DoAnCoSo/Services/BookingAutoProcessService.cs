using DoAnCoSo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity.UI.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DoAnCoSo.Services
{
    public class BookingAutoProcessService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BookingAutoProcessService> _logger;

        public BookingAutoProcessService(IServiceProvider serviceProvider, ILogger<BookingAutoProcessService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                        
                        var now = DateTime.Now;
                        bool hasChanges = false;

                        // 1. Tự động chuyển trạng thái "Đang phục vụ" trước 15 phút
                        var targetTime15 = now.AddMinutes(15);
                        var windowStart15 = targetTime15.AddMinutes(-5);

                        var upcomingBookings = await context.Bookings
                            .Include(b => b.Table)
                            .Where(b => b.Status == "Confirmed" 
                                     && b.TableId != null
                                     && b.BookingDate <= targetTime15
                                     && b.BookingDate >= windowStart15)
                            .ToListAsync(stoppingToken);

                        foreach (var booking in upcomingBookings)
                        {
                            var order = await context.Orders
                                .FirstOrDefaultAsync(o => o.BookingId == booking.BookingId, stoppingToken);

                            if (order != null && (order.Status == "Pending" || order.Status == "PreOrder"))
                            {
                                order.Status = "Processing";
                                _logger.LogInformation($"Auto-processing Order {order.OrderId} for Booking {booking.BookingId} (15 mins prior).");
                                hasChanges = true;
                            }

                            if (booking.Table != null && booking.Table.Status != "Occupied")
                            {
                                booking.Table.Status = "Occupied";
                                hasChanges = true;
                            }
                        }

                        // 2. Cảnh báo trễ 30 phút
                        var late30Time = now.AddMinutes(-30);
                        var warnings = await context.Bookings
                            .Where(b => (b.Status == "Confirmed" || b.Status == "Pending") 
                                     && b.BookingDate <= late30Time
                                     && (b.Note == null || !b.Note.Contains("[WarningSent]")))
                            .ToListAsync(stoppingToken);

                        foreach (var b in warnings)
                        {
                            b.Note = (b.Note + " [WarningSent]").Trim();
                            hasChanges = true;

                            if (!string.IsNullOrEmpty(b.Email))
                            {
                                try 
                                {
                                    string subject = $"CẢNH BÁO TRỄ HẸN ĐẶT BÀN - MÃ: {b.CheckInCode}";
                                    string content = $@"
                                        <h3>Xin chào {b.FullName},</h3>
                                        <p>Đơn đặt bàn của bạn tại Manwah (Mã check-in: <strong>{b.CheckInCode}</strong>) đã quá giờ hẹn 30 phút.</p>
                                        <p>Nếu sau 30 phút nữa (tức là trễ tổng cộng 60 phút) bạn không có mặt để nhận bàn, hệ thống sẽ tự động <strong>HỦY ĐƠN</strong> và bạn sẽ mất khoản tiền cọc theo quy định của nhà hàng.</p>
                                        <p>Vui lòng đến ngay nhà hàng hoặc liên hệ Hotline để được hỗ trợ.</p>
                                        <br/><p>Trân trọng,<br/>Đội ngũ Manwah</p>";
                                    await emailSender.SendEmailAsync(b.Email, subject, content);
                                    _logger.LogInformation($"Sent 30-min late warning for Booking {b.BookingId}.");
                                }
                                catch (Exception emailEx)
                                {
                                    _logger.LogError(emailEx, $"Failed to send 30-min warning email to {b.Email}");
                                }
                            }
                        }

                        // 3. Tự động hủy nếu trễ 60 phút
                        var late60Time = now.AddMinutes(-60);
                        var cancels = await context.Bookings
                            .Include(b => b.Table)
                            .Where(b => (b.Status == "Confirmed" || b.Status == "Pending") 
                                     && b.BookingDate <= late60Time)
                            .ToListAsync(stoppingToken);

                        foreach (var b in cancels)
                        {
                            b.Status = "Cancelled";
                            if (b.Table != null) b.Table.Status = "Empty";
                            hasChanges = true;

                            var order = await context.Orders.FirstOrDefaultAsync(o => o.BookingId == b.BookingId, stoppingToken);
                            if (order != null && (order.Status == "Pending" || order.Status == "Processing" || order.Status == "PreOrder"))
                            {
                                order.Status = "Cancelled";
                            }

                            if (!string.IsNullOrEmpty(b.Email))
                            {
                                try
                                {
                                    string subject = $"THÔNG BÁO HỦY ĐƠN ĐẶT BÀN - MÃ: {b.CheckInCode}";
                                    string content = $@"
                                        <h3>Xin chào {b.FullName},</h3>
                                        <p>Đơn đặt bàn của bạn tại Manwah (Mã check-in: <strong>{b.CheckInCode}</strong>) đã bị <strong>HỦY</strong> do bạn trễ hẹn quá 60 phút.</p>
                                        <p>Theo quy định của nhà hàng, số tiền cọc của bạn sẽ không được hoàn lại.</p>
                                        <p>Cảm ơn bạn đã quan tâm đến dịch vụ của chúng tôi.</p>
                                        <br/><p>Trân trọng,<br/>Đội ngũ Manwah</p>";
                                    await emailSender.SendEmailAsync(b.Email, subject, content);
                                    _logger.LogInformation($"Cancelled Booking {b.BookingId} for being 60 mins late.");
                                }
                                catch (Exception emailEx)
                                {
                                    _logger.LogError(emailEx, $"Failed to send 60-min cancel email to {b.Email}");
                                }
                            }
                        }

                        if (hasChanges)
                        {
                            await context.SaveChangesAsync(stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing BookingAutoProcessService.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
