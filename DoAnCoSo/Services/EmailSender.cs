using DoAnCoSo.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DoAnCoSo.Services
{
    public class EmailSender
    {
        private readonly EmailSettings _emailSettings;

        // Dùng IOptions để đọc cấu hình từ appsettings.json hoặc User Secrets
        public EmailSender(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        /// <summary>
        /// Hàm gửi Email chung
        /// </summary>
        /// <param name="email">Địa chỉ người nhận</param>
        /// <param name="subject">Tiêu đề thư</param>
        /// <param name="htmlMessage">Nội dung thư dạng HTML</param>
        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var message = new MimeMessage();

            // 1. Cấu hình người gửi (Tên hiển thị và Email gửi)
            message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));

            // 2. Cấu hình người nhận
            message.To.Add(MailboxAddress.Parse(email));

            // 3. Tiêu đề
            message.Subject = subject;

            // 4. Nội dung (Dùng BodyBuilder để tạo nội dung HTML)
            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlMessage
            };
            message.Body = bodyBuilder.ToMessageBody();

            // 5. Tiến hành gửi mail bằng SmtpClient của MailKit
            using var client = new SmtpClient();
            try
            {
                // Kết nối tới server Google (smtp.gmail.com - Cổng 465 dùng SSL)
                await client.ConnectAsync(_emailSettings.MailServer, _emailSettings.MailPort, SecureSocketOptions.SslOnConnect);

                // Đăng nhập bằng tài khoản và Mật khẩu ứng dụng (16 ký tự)
                await client.AuthenticateAsync(_emailSettings.SenderEmail, _emailSettings.Password);

                // Gửi mail
                await client.SendAsync(message);
            }
            catch (Exception ex)
            {
                // Nếu lỗi (ví dụ sai mật khẩu hoặc mất mạng), ghi log ra console để debug
                Console.WriteLine("LỖI GỬI EMAIL: " + ex.Message);
                throw; // Ném lỗi ra ngoài để Controller có thể bắt được nếu cần
            }
            finally
            {
                // Ngắt kết nối an toàn
                await client.DisconnectAsync(true);
            }
        }
    }
}