using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Utils;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace EVServiceCenterMaintenanceAPI.Services
{
    public class EmailService
    {
        private readonly EmailSetting _emailSetting;
        public EmailService(IOptions<EmailSetting> emailSetting)
        {
            _emailSetting = emailSetting.Value;
        }
        public async Task SendEmailAsync(string toEmail, string subject, string body, bool isBodyHtml = true)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                throw new ArgumentException("Recipient email cannot be empty.", nameof(toEmail));
            if (string.IsNullOrWhiteSpace(subject))
                throw new ArgumentException("Subject cannot be empty.", nameof(subject));
            if (string.IsNullOrWhiteSpace(body))
                throw new ArgumentException("Body cannot be empty.", nameof(body));

            if (_emailSetting.UseApi)
            {
                if (string.IsNullOrWhiteSpace(_emailSetting.Password))
                    throw new InvalidOperationException("SendGrid API Key is not configured.");

                await SendEmailViaSendGridApiAsync(toEmail, subject, body, isBodyHtml);
                return;
            }

            await SendEmailViaSmtpAsync(toEmail, subject, body, isBodyHtml);
        }

        private async Task SendEmailViaSmtpAsync(string toEmail, string subject, string body, bool isBodyHtml)
        {
            if (string.IsNullOrWhiteSpace(_emailSetting.Server) || _emailSetting.Port == 0 || string.IsNullOrWhiteSpace(_emailSetting.Username) || string.IsNullOrWhiteSpace(_emailSetting.Password))
                throw new InvalidOperationException("SMTP configuration is incomplete.");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailSetting.Sender ?? "EV Service Center", _emailSetting.Username));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder();
            if (isBodyHtml)
                bodyBuilder.HtmlBody = body;
            else
                bodyBuilder.TextBody = body;
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            client.Timeout = 30000; // 30 seconds

            try
            {
                await client.ConnectAsync(_emailSetting.Server, _emailSetting.Port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_emailSetting.Username, _emailSetting.Password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                Console.WriteLine($"Email sent successfully via SMTP to {toEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SMTP Error: {ex.Message}");
                throw new InvalidOperationException($"SMTP failed: {ex.Message}");
            }
        }

        public async Task<bool> SendActivationEmailAsync(string userName, string toEmail, string linkActivate, DateTime expiryDate)
        {
            try
            {
                var message = EmailTemplate.GenerateActivationEmailTemplate(userName, linkActivate, expiryDate);
                await SendEmailAsync(toEmail, "Kích hoạt tài khoản của bạn", message, true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> SendUserCreatedEmailAsync(UserResponseDto user, string password, string recipientEmail)
        {

            try
            {
                var emailTemplate = EmailTemplate.GenerateUserCreatedEmailTemplate(user, password);
                await SendEmailAsync(recipientEmail, "Thông báo tạo tài khoản mới", emailTemplate);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> SendOtpEmailAsync(string userName, string toEmail, string otpCode, DateTime expiryDate)
        {
            try
            {
                var message = EmailTemplate.GenerateOtpEmailTemplate(userName, otpCode, expiryDate);
                await SendEmailAsync(toEmail, "Mã OTP của bạn", message, true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task SendEmailViaSendGridApiAsync(string toEmail, string subject, string body, bool isBodyHtml)
        {
            var apiKey = _emailSetting.Password; // SendGrid API Key
            var client = new SendGridClient(apiKey);

            var from = new EmailAddress("nvkhang0099@gmail.com", _emailSetting.Sender ?? "EV Service Center");
            var to = new EmailAddress(toEmail);

            // Create email message
            SendGridMessage msg;
            if (isBodyHtml)
            {
                msg = MailHelper.CreateSingleEmail(from, to, subject, null, body);
            }
            else
            {
                msg = MailHelper.CreateSingleEmail(from, to, subject, body, null);
            }

            var response = await client.SendEmailAsync(msg);

            if (response.StatusCode != System.Net.HttpStatusCode.Accepted)
            {
                var errorBody = await response.Body.ReadAsStringAsync();
                throw new InvalidOperationException($"SendGrid API failed: {response.StatusCode} - {errorBody}");
            }

            Console.WriteLine($"Email sent successfully via SendGrid API to {toEmail}");
        }
    }
}
