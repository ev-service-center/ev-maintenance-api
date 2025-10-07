using System.Net.Mail;
using System.Net;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Utils;
using Microsoft.Extensions.Options;

namespace EVServiceCenterMaintenanceAPI.Services
{
    public class EmailService
    {
        private readonly EmailSetting _emailSetting;
        private readonly SmtpClient _smtpClient;
        public EmailService(IOptions<EmailSetting> emailSetting)
        {
            _emailSetting = emailSetting.Value;
            _smtpClient = new SmtpClient(_emailSetting.Server)
            {
                Port = _emailSetting.Port,
                Credentials = new NetworkCredential(_emailSetting.Username, _emailSetting.Password),
                EnableSsl = true,
            };
        }
        public async Task SendEmailAsync(string toEmail, string subject, string body, bool isBodyHtml = true)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                throw new ArgumentException("Recipient email cannot be empty.", nameof(toEmail));
            if (string.IsNullOrWhiteSpace(subject))
                throw new ArgumentException("Subject cannot be empty.", nameof(subject));
            if (string.IsNullOrWhiteSpace(body))
                throw new ArgumentException("Body cannot be empty.", nameof(body));

            if (string.IsNullOrWhiteSpace(_emailSetting.Server) || _emailSetting.Port == 0 || string.IsNullOrWhiteSpace(_emailSetting.Username) || string.IsNullOrWhiteSpace(_emailSetting.Password))
                throw new InvalidOperationException("SMTP configuration is incomplete.");


            var mailMessage = new MailMessage
            {
                From = new MailAddress(_emailSetting.Username),
                Subject = subject,
                Body = body,
                IsBodyHtml = isBodyHtml,
            };
            mailMessage.To.Add(toEmail);

            await _smtpClient.SendMailAsync(mailMessage);
        }

        public async Task<bool> SendActivationEmailAsync(string userName, string toEmail, string linkActivate, DateTime expiryDate)
        {
            try
            {
                var message = EmailTemplate.GenerateActivationEmailTemplate(userName, linkActivate, expiryDate);
                await SendEmailAsync(toEmail, "Mã OTP của bạn", message, true);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
