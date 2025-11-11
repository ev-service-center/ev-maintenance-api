using System.Net;
using System.Text.RegularExpressions;
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
    public partial class EmailService
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

        public async Task<bool> SendChangeInfoAppointmentEmailAsync(AppointmentResponseDto app, string? toEmail)
        {
            try
            {
                if (string.IsNullOrEmpty(toEmail))
                {
                    return false;
                }

                var message = EmailTemplate.GenerateMaintenanceStatusEmailTemplate(app);
                await SendEmailAsync(toEmail, "Thông báo trạng thái bảo dưỡng xe - 3DO Corp", message, true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> SendDepositPaymentConfirmationEmailAsync(
            string customerName,
            string toEmail,
            int workOrderId,
            decimal depositAmount,
            string vehicleInfo,
            string appointmentDate,
            string paymentDate,
            string transactionId,
            string orderCode)
        {
            try
            {
                if (string.IsNullOrEmpty(toEmail))
                {
                    return false;
                }

                var message = EmailTemplate.GenerateDepositPaymentConfirmationEmailTemplate(
                    customerName,
                    workOrderId,
                    depositAmount,
                    vehicleInfo,
                    appointmentDate,
                    paymentDate,
                    transactionId,
                    orderCode);

                await SendEmailAsync(toEmail, "Xác nhận thanh toán cọc - EV Service Center", message, true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> SendFinalPaymentConfirmationEmailAsync(
            string customerName,
            string toEmail,
            int invoiceId,
            int workOrderId,
            decimal finalAmount,
            decimal totalAmount,
            decimal totalPaid,
            string vehicleInfo,
            string paymentDate,
            string transactionId,
            string orderCode,
            string invoiceStatus)
        {
            try
            {
                if (string.IsNullOrEmpty(toEmail))
                {
                    return false;
                }

                var message = EmailTemplate.GenerateFinalPaymentConfirmationEmailTemplate(
                    customerName,
                    invoiceId,
                    workOrderId,
                    finalAmount,
                    totalAmount,
                    totalPaid,
                    vehicleInfo,
                    paymentDate,
                    transactionId,
                    orderCode,
                    invoiceStatus);

                await SendEmailAsync(toEmail, "Xác nhận thanh toán cuối - EV Service Center", message, true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> SendMaintenanceHistoryCreatedEmailAsync(MaintenanceHistoryResponseDto history, string toEmail)
        {
            try
            {
                var message = EmailTemplate.GenerateMaintenanceHistoryEmailTemplate(history);
                await SendEmailAsync(toEmail, "Thông báo lịch sử bảo dưỡng xe - 3DO Corp", message, true);
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

            var from = new EmailAddress(_emailSetting.Username ?? "noreply@evservicecenter.me",
                                        _emailSetting.Sender ?? "EV Service Center");
            var to = new EmailAddress(toEmail);

            // Create email message
            SendGridMessage msg;
            if (isBodyHtml)
            {
                var plainText = StripHtml(body);
                msg = MailHelper.CreateSingleEmail(from, to, subject, plainText, body);
            }
            else
            {
                msg = MailHelper.CreateSingleEmail(from, to, subject, body, null);
            }

            msg.SetReplyTo(new EmailAddress(_emailSetting.ReplyTo ?? "support@evservicecenter.me", "Support Team"));

            var response = await client.SendEmailAsync(msg);

            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                var errorBody = await response.Body.ReadAsStringAsync();
                throw new InvalidOperationException($"SendGrid API failed: {response.StatusCode} - {errorBody}");
            }

            Console.WriteLine($"Email sent successfully via SendGrid API to {toEmail}");
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            var plainText = HtmlRegex().Replace(html, string.Empty);
            return WebUtility.HtmlDecode(plainText).Trim();
        }

        [GeneratedRegex("<.*?>")]
        private static partial Regex HtmlRegex();
    }
}
