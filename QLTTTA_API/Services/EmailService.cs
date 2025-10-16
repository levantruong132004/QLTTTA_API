using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace QLTTTA_API.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                throw new ArgumentException("Địa chỉ email đích không hợp lệ.");

            var smtpHost = _config["Email:SmtpHost"] ?? "smtp.gmail.com";
            var smtpPort = int.TryParse(_config["Email:SmtpPort"], out var port) ? port : 587;
            var smtpUser = _config["Email:SmtpUser"];
            var smtpPass = _config["Email:SmtpPass"];
            var fromName = _config["Email:FromName"] ?? "QLTTTA";
            var fromEmail = _config["Email:FromEmail"] ?? smtpUser;
            var useTls = bool.TryParse(_config["Email:UseStartTls"], out var tls) && tls;

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = useTls
            };

            var mail = new MailMessage
            {
                From = new MailAddress(fromEmail!, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            mail.To.Add(toEmail);

            try
            {
                await client.SendMailAsync(mail);
                _logger.LogInformation("✅ Gửi email tới {Email} thành công.", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Gửi email thất bại tới {Email}", toEmail);
                throw new Exception($"Không thể gửi email đến {toEmail}: {ex.Message}");
            }
        }
    }
}
