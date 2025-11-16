using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace QLTTTA_API.Services
{
    public interface IEmailService
    {
        Task SendAsync(string toEmail, string subject, string htmlBody, IEnumerable<(string fileName, byte[] content, string contentType)>? attachments = null, CancellationToken ct = default);
    }

    public class MailKitEmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<MailKitEmailService> _logger;

        public MailKitEmailService(IConfiguration config, ILogger<MailKitEmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendAsync(string toEmail, string subject, string htmlBody, IEnumerable<(string fileName, byte[] content, string contentType)>? attachments = null, CancellationToken ct = default)
        {
            // Prefer Smtp:*; fallback to Email:*
            var host = _config["Smtp:Host"] ?? _config["Email:SmtpHost"];
            var portStr = _config["Smtp:Port"] ?? _config["Email:SmtpPort"];
            var user = _config["Smtp:User"] ?? _config["Email:SmtpUser"];
            var pass = _config["Smtp:Pass"] ?? _config["Email:SmtpPass"];
            var fromEmail = _config["Smtp:From"] ?? _config["Email:FromEmail"] ?? user;
            var fromName = _config["Smtp:FromName"] ?? _config["Email:FromName"] ?? "LDA Center";

            bool enableStartTls = true;
            var sslStr = _config["Smtp:Ssl"]; // true => use StartTls, false => Auto
            if (!string.IsNullOrWhiteSpace(sslStr))
            {
                enableStartTls = sslStr.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                var useStartTls = _config["Email:UseStartTls"];
                if (!string.IsNullOrWhiteSpace(useStartTls))
                {
                    enableStartTls = useStartTls.Equals("true", StringComparison.OrdinalIgnoreCase);
                }
            }

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            {
                _logger.LogWarning("SMTP settings are missing. Email not sent. Subject={Subject}, To={To}", subject, toEmail);
                return;
            }

            int port = 587;
            if (!string.IsNullOrWhiteSpace(portStr) && int.TryParse(portStr, out var p)) port = p;

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
            if (attachments != null)
            {
                foreach (var att in attachments)
                {
                    bodyBuilder.Attachments.Add(att.fileName, att.content, ContentType.Parse(att.contentType));
                }
            }
            message.Body = bodyBuilder.ToMessageBody();

            using var smtp = new SmtpClient();
            try
            {
                await smtp.ConnectAsync(host, port, enableStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, ct);
                await smtp.AuthenticateAsync(user, pass, ct);
                await smtp.SendAsync(message, ct);
                await smtp.DisconnectAsync(true, ct);
                _logger.LogInformation("Sent email to {To}: {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To} with subject {Subject}", toEmail, subject);
            }
        }
    }
}
