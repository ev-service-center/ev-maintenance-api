using System.Net.Mail;
using System.Net;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Models;

namespace EVServiceCenterMaintenanceAPI.Services
{
    public class EmailService
    {
        public async Task SendEmailAsync(string toEmail, string subject, string body, bool isBodyHtml = true)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                throw new ArgumentException("Recipient email cannot be empty.", nameof(toEmail));
            if (string.IsNullOrWhiteSpace(subject))
                throw new ArgumentException("Subject cannot be empty.", nameof(subject));
            if (string.IsNullOrWhiteSpace(body))
                throw new ArgumentException("Body cannot be empty.", nameof(body));

            var smtpHost = "smtp.gmail.com";
            var smtpPort = 587;
            var smtpUsername = "nvkhang0099@gmail.com";
            var smtpPassword = "mzljsjirybeaazai";

            if (string.IsNullOrWhiteSpace(smtpHost) || smtpPort == 0 || string.IsNullOrWhiteSpace(smtpUsername) || string.IsNullOrWhiteSpace(smtpPassword))
                throw new InvalidOperationException("SMTP configuration is incomplete.");

            var smtpClient = new SmtpClient(smtpHost)
            {
                Port = smtpPort,
                Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(smtpUsername),
                Subject = subject,
                Body = body,
                IsBodyHtml = isBodyHtml,
            };
            mailMessage.To.Add(toEmail);

            await smtpClient.SendMailAsync(mailMessage);
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
