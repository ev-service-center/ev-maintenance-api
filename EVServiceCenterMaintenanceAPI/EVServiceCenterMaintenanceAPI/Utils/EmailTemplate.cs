using System;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Models;

namespace EVServiceCenterMaintenanceAPI.Utils
{
    public static class EmailTemplate
    {
        private static DateTime GetVietNamTime(DateTime utcTime)
        {
            TimeZoneInfo timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            DateTime vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, timeZoneInfo);
            return vietnamTime;
        }
        public static string GenerateActivationEmailTemplate(string userName, string activationLink, DateTime expiryDate)
        {
            var vietnamExpiryDate = GetVietNamTime(expiryDate);
            return @"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Kích hoạt tài khoản EV Service Center</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; background: #f4f7fa; padding: 20px; }
        .email-container { max-width: 600px; margin: 0 auto; background: #ffffff; border-radius: 12px; box-shadow: 0 8px 24px rgba(0, 0, 0, 0.1); overflow: hidden; }
        .header { background: linear-gradient(135deg, #007bff, #00c4cc); padding: 30px; text-align: center; position: relative; }
        .header img { width: 100px; height: auto; margin-bottom: 15px; }
        .header h1 { color: #ffffff; font-size: 24px; font-weight: 600; margin-bottom: 10px; }
        .header p { color: rgba(255, 255, 255, 0.9); font-size: 14px; }
        .content { padding: 40px; text-align: center; }
        .greeting { font-size: 18px; color: #1a2b49; margin-bottom: 20px; font-weight: 600; }
        .message { font-size: 15px; color: #4a5b6c; margin-bottom: 30px; line-height: 1.7; }
        .action-button { display: inline-block; padding: 12px 24px; background: #007bff; color: #ffffff; font-weight: 600; text-decoration: none; border-radius: 8px; margin: 20px 0; }
        .action-button:hover { background: #0056b3; }
        .expiry-info { margin-top: 20px; padding: 15px; background: #fff8e1; border-left: 4px solid #ffca28; border-radius: 8px; text-align: left; }
        .expiry-info h3 { color: #e65100; font-size: 14px; margin-bottom: 8px; font-weight: 600; }
        .expiry-info p { color: #4a5b6c; font-size: 13px; }
        .security-notice { margin: 20px 0; padding: 15px; background: #ffebee; border-left: 4px solid #d32f2f; border-radius: 8px; text-align: left; }
        .security-notice h3 { color: #b71c1c; font-size: 14px; margin-bottom: 8px; font-weight: 600; }
        .security-notice p { color: #4a5b6c; font-size: 13px; }
        .footer { background: #f8f9fa; padding: 30px; text-align: center; border-top: 1px solid #e0e4e8; }
        .footer-brand { font-size: 18px; font-weight: 700; color: #007bff; margin-bottom: 10px; }
        .footer p { color: #6c757d; font-size: 12px; margin-bottom: 8px; }
        .footer a { color: #007bff; text-decoration: none; }
        .footer a:hover { text-decoration: underline; }
        @media (max-width: 600px) {
            .email-container { margin: 10px; border-radius: 8px; }
            .header { padding: 20px; }
            .content { padding: 20px; }
            .header img { width: 80px; }
            .header h1 { font-size: 20px; }
            .action-button { padding: 10px 20px; font-size: 14px; }
            .footer { padding: 20px; }
        }
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <img src='https://res.cloudinary.com/dphys6egj/image/upload/v1759812317/Pngtree_hipster_bike_electric_logo_design_4847419_wvci4k.jpg' alt='Logo EV Service Center'>
            <h1>Chào mừng đến với EV Service Center!</h1>
            <p>Kích hoạt tài khoản của bạn để bắt đầu</p>
        </div>
        <div class='content'>
            <div class='greeting'>Xin chào, " + userName + @"!</div>
            <div class='message'>
                Cảm ơn bạn đã tham gia EV Service Center. Vui lòng nhấp vào nút bên dưới để kích hoạt tài khoản và bắt đầu khám phá các dịch vụ của chúng tôi.
            </div>
            <a href='" + activationLink + @"' class='action-button'>Kích hoạt tài khoản</a>
            <div class='expiry-info'>
                <h3>Lưu ý quan trọng</h3>
                <p>Liên kết kích hoạt này sẽ hết hạn vào lúc <strong>" + vietnamExpiryDate.ToString("dd/MM/yyyy HH:mm:ss") + @"</strong>. Vui lòng kích hoạt tài khoản của bạn ngay lập tức.</p>
            </div>
            <div class='security-notice'>
                <h3>Thông báo bảo mật</h3>
                <p>Nếu bạn không đăng ký tài khoản EV Service Center, vui lòng bỏ qua email này và liên hệ với đội ngũ hỗ trợ của chúng tôi tại <a href='mailto:support@evservicecenter.me'>support@evservicecenter.me</a>.</p>
            </div>
        </div>
        <div class='footer'>
            <div class='footer-brand'>EV Service Center</div>
            <p>Tra Vinh, Viet Nam</p>
            <p>Email: <a href='mailto:support@evservicecenter.me'>support@evservicecenter.me</a> | Điện thoại: 0338302160</p>
            <p>© 2025 EV Service Center. Tất cả quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";
        }

        public static string GenerateOtpEmailTemplate(string userName, string otpCode, DateTime expiryDate)
        {
            var vietnamExpiryDate = GetVietNamTime(expiryDate);
            return @"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>MUSIC - Đặt lại mật khẩu</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; background: #f4f7fa; padding: 20px; }
        .email-container { max-width: 600px; margin: 0 auto; background: #ffffff; border-radius: 12px; box-shadow: 0 8px 24px rgba(0, 0, 0, 0.1); overflow: hidden; }
        .header { background: linear-gradient(135deg, #007bff, #00c4cc); padding: 30px; text-align: center; position: relative; }
        .header img { width: 100px; height: auto; margin-bottom: 15px; }
        .header h1 { color: #ffffff; font-size: 24px; font-weight: 600; margin-bottom: 10px; }
        .header p { color: rgba(255, 255, 255, 0.9); font-size: 14px; }
        .content { padding: 40px; text-align: center; }
        .greeting { font-size: 18px; color: #1a2b49; margin-bottom: 20px; font-weight: 600; }
        .message { font-size: 15px; color: #4a5b6c; margin-bottom: 30px; line-height: 1.7; }
        .otp-container { background: #e3f2fd; border-radius: 8px; padding: 20px; margin: 20px 0; }
        .otp-label { color: #1a2b49; font-size: 14px; font-weight: 600; margin-bottom: 10px; }
        .otp-code { font-size: 28px; font-weight: 700; color: #007bff; letter-spacing: 5px; font-family: monospace; background: #ffffff; padding: 15px; border-radius: 6px; border: 1px solid #e0e4e8; }
        .expiry-info { margin-top: 20px; padding: 15px; background: #fff8e1; border-left: 4px solid #ffca28; border-radius: 8px; text-align: left; }
        .expiry-info h3 { color: #e65100; font-size: 14px; margin-bottom: 8px; font-weight: 600; }
        .expiry-info p { color: #4a5b6c; font-size: 13px; }
        .security-notice { margin: 20px 0; padding: 15px; background: #ffebee; border-left: 4px solid #d32f2f; border-radius: 8px; text-align: left; }
        .security-notice h3 { color: #b71c1c; font-size: 14px; margin-bottom: 8px; font-weight: 600; }
        .security-notice p { color: #4a5b6c; font-size: 13px; }
        .footer { background: #f8f9fa; padding: 30px; text-align: center; border-top: 1px solid #e0e4e8; }
        .footer-brand { font-size: 18px; font-weight: 700; color: #007bff; margin-bottom: 10px; }
        .footer p { color: #6c757d; font-size: 12px; margin-bottom: 8px; }
        .footer a { color: #007bff; text-decoration: none; }
        .footer a:hover { text-decoration: underline; }
        @media (max-width: 600px) {
            .email-container { margin: 10px; border-radius: 8px; }
            .header { padding: 20px; }
            .content { padding: 20px; }
            .header img { width: 80px; }
            .header h1 { font-size: 20px; }
            .action-button { padding: 10px 20px; font-size: 14px; }
            .footer { padding: 20px; }
        }
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <img src='https://res.cloudinary.com/dphys6egj/image/upload/v1759812317/Pngtree_hipster_bike_electric_logo_design_4847419_wvci4k.jpg' alt='Logo EV Service Center'>
            <h1>Yêu cầu đặt lại mật khẩu</h1>
            <p>Xác minh danh tính của bạn</p>
        </div>
        <div class='content'>
            <div class='greeting'>Xin chào, " + userName + @"!</div>
            <div class='message'>
                Chúng tôi nhận được yêu cầu đặt lại mật khẩu của bạn. Sử dụng Mã OTP (One-Time Password) bên dưới để tiếp tục quá trình đặt lại mật khẩu.
            </div>
            <div class='otp-container'>
                <div class='otp-label'>Mã OTP của bạn:</div>
                <div class='otp-code'>" + otpCode + @"</div>
            </div>
            <div class='expiry-info'>
                <h3>Lưu ý quan trọng</h3>
                <p>Mã OTP này sẽ hết hạn vào lúc <strong>" + vietnamExpiryDate.ToString("dd/MM/yyyy HH:mm:ss") + @"</strong>. Vui lòng hoàn tất việc đặt lại mật khẩu ngay lập tức.</p>
            </div>
            <div class='security-notice'>
                <h3>Thông báo bảo mật</h3>
                <p>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này và liên hệ với đội ngũ hỗ trợ của chúng tôi tại <a href='mailto:support@evservicecenter.me'>support@evservicecenter.me</a>.</p>
            </div>
        </div>
        <div class='footer'>
            <div class='footer-brand'>EV Service Center</div>
            <p>Tra Vinh, Viet Nam</p>
            <p>Email: <a href='mailto:support@evservicecenter.me'>support@evservicecenter.me</a> | Điện thoại: 0338302160</p>
            <p>© 2025 EV Service Center. Tất cả quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";
        }

        public static string GenerateUserCreatedEmailTemplate(UserResponseDto user, string password)
        {
            string fullName = user.FullName;
            string username = user.Username;
            string email = user.Email ?? "N/A";
            string phone = user.Phone ?? "N/A";
            string role = user.Role.ToString();
            string status = user.Status.ToString();
            string createdAt = GetVietNamTime(user.CreatedAt).ToString("dd/MM/yyyy HH:mm");

            return @"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Thông báo tạo tài khoản</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; background: #f4f7fa; padding: 20px; }
        .email-container { max-width: 600px; margin: 0 auto; background: #ffffff; border-radius: 12px; box-shadow: 0 8px 24px rgba(0, 0, 0, 0.1); overflow: hidden; }
        .header { background: linear-gradient(135deg, #007bff, #00c4cc); padding: 30px; text-align: center; position: relative; }
        .header img { width: 100px; height: auto; margin-bottom: 15px; }
        .header h1 { color: #ffffff; font-size: 24px; font-weight: 600; margin-bottom: 10px; }
        .header p { color: rgba(255, 255, 255, 0.9); font-size: 14px; }
        .content { padding: 40px; text-align: center; }
        .greeting { font-size: 18px; color: #1a2b49; margin-bottom: 20px; font-weight: 600; }
        .message { font-size: 15px; color: #4a5b6c; margin-bottom: 30px; line-height: 1.7; }
        .user-details { margin: 20px 0; padding: 15px; background: #f8f9fa; border-radius: 8px; text-align: left; }
        .user-details h3 { color: #1a2b49; font-size: 16px; margin-bottom: 10px; font-weight: 600; }
        .user-details p { color: #4a5b6c; font-size: 14px; margin-bottom: 8px; }
        .expiry-info { margin-top: 20px; padding: 15px; background: #fff8e1; border-left: 4px solid #ffca28; border-radius: 8px; text-align: left; }
        .expiry-info h3 { color: #e65100; font-size: 14px; margin-bottom: 8px; font-weight: 600; }
        .expiry-info p { color: #4a5b6c; font-size: 13px; }
        .security-notice { margin: 20px 0; padding: 15px; background: #ffebee; border-left: 4px solid #d32f2f; border-radius: 8px; text-align: left; }
        .security-notice h3 { color: #b71c1c; font-size: 14px; margin-bottom: 8px; font-weight: 600; }
        .security-notice p { color: #4a5b6c; font-size: 13px; }
        .footer { background: #f8f9fa; padding: 30px; text-align: center; border-top: 1px solid #e0e4e8; }
        .footer-brand { font-size: 18px; font-weight: 700; color: #007bff; margin-bottom: 10px; }
        .footer p { color: #6c757d; font-size: 12px; margin-bottom: 8px; }
        .footer a { color: #007bff; text-decoration: none; }
        .footer a:hover { text-decoration: underline; }
        @media (max-width: 600px) {
            .email-container { margin: 10px; border-radius: 8px; }
            .header { padding: 20px; }
            .content { padding: 20px; }
            .header img { width: 80px; }
            .header h1 { font-size: 20px; }
            .action-button { padding: 10px 20px; font-size: 14px; }
            .footer { padding: 20px; }
        }
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <img src='https://res.cloudinary.com/dphys6egj/image/upload/v1759812317/Pngtree_hipster_bike_electric_logo_design_4847419_wvci4k.jpg' alt='Logo EV Maintenance'>
            <h1>Chào mừng đến với EV Service Center!</h1>
            <p>Thông báo tạo tài khoản</p>
        </div>
        <div class='content'>
            <div class='greeting'>Xin chào, " + fullName + @"!</div>
            <div class='message'>
                Tài khoản của bạn đã được tạo thành công. Dưới đây là thông tin đăng nhập và chi tiết tài khoản của bạn. Vui lòng đổi mật khẩu sau khi đăng nhập lần đầu.
            </div>
            <div class='user-details'>
                <h3>Thông tin tài khoản</h3>
                <p><strong>Tên người dùng:</strong> " + username + @"</p>
                <p><strong>Mật khẩu tạm thời:</strong> " + password + @"</p>
                <p><strong>Họ và tên:</strong> " + fullName + @"</p>
                <p><strong>Email:</strong> " + email + @"</p>
                <p><strong>Số điện thoại:</strong> " + phone + @"</p>
                <p><strong>Vai trò:</strong> " + role + @"</p>
                <p><strong>Trạng thái:</strong> " + status + @"</p>
                <p><strong>Ngày tạo:</strong> " + createdAt + @"</p>
            </div>
            <div class='security-notice'>
                <h3>Thông báo bảo mật</h3>
                <p>Đây là mật khẩu tạm thời. Vui lòng đăng nhập và đổi mật khẩu ngay lập tức. Nếu bạn không yêu cầu tạo tài khoản này, vui lòng liên hệ với đội ngũ hỗ trợ của chúng tôi tại <a href='mailto:support@evservicecenter.me'>support@evservicecenter.me</a>.</p>
            </div>
        </div>
        <div class='footer'>
            <div class='footer-brand'>EV Service Center</div>
            <p>Tra Vinh, Viet Nam</p>
            <p>Email: <a href='mailto:support@evservicecenter.me'>support@evservicecenter.me</a> | Điện thoại: 0338302160</p>
            <p>© 2025 EV Service Center. Tất cả quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";
        }

        public static string GenerateMaintenanceStatusEmailTemplate(AppointmentResponseDto appointment)
        {
            string userName = appointment.CustomerDetails?.FullName ?? "Khách hàng";
            string vehicleInfo = $"{appointment.VehicleDetails?.Model ?? "N/A"} ({appointment.VehicleDetails?.Plate ?? "N/A"})";
            string status = appointment.Status.ToString();
            string appointmentDate = appointment.AppointmentDate.ToString("dd/MM/yyyy HH:mm");
            string serviceNames = appointment.WorkOrderDetails?.ServiceDetails != null
                ? string.Join(", ", appointment.WorkOrderDetails.ServiceDetails.Select(s => s.ServiceName))
                : "Không có dịch vụ";
            string centerName = appointment.CenterDetails?.CenterName ?? "N/A";
            string centerAddress = appointment.CenterDetails?.Address ?? "N/A";
            string slotTime = appointment.SlotDetails?.StartTime.ToString("HH:mm") ?? "N/A";

            return @"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Thông báo trạng thái bảo dưỡng xe</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; background: #f4f7fa; padding: 20px; }
        .email-container { max-width: 600px; margin: 0 auto; background: #ffffff; border-radius: 12px; box-shadow: 0 8px 24px rgba(0, 0, 0, 0.1); overflow: hidden; }
        .header { background: linear-gradient(135deg, #007bff, #00c4cc); padding: 30px; text-align: center; position: relative; }
        .header img { width: 100px; height: auto; margin-bottom: 15px; }
        .header h1 { color: #ffffff; font-size: 24px; font-weight: 600; margin-bottom: 10px; }
        .header p { color: rgba(255, 255, 255, 0.9); font-size: 14px; }
        .content { padding: 40px; text-align: center; }
        .greeting { font-size: 18px; color: #1a2b49; margin-bottom: 20px; font-weight: 600; }
        .message { font-size: 15px; color: #4a5b6c; margin-bottom: 30px; line-height: 1.7; }
        .appointment-details { margin: 20px 0; padding: 15px; background: #f8f9fa; border-radius: 8px; text-align: left; }
        .appointment-details h3 { color: #1a2b49; font-size: 16px; margin-bottom: 10px; font-weight: 600; }
        .appointment-details p { color: #4a5b6c; font-size: 14px; margin-bottom: 8px; }
        .status-info { margin-top: 20px; padding: 15px; background: #fff8e1; border-left: 4px solid #ffca28; border-radius: 8px; text-align: left; }
        .status-info h3 { color: #e65100; font-size: 14px; margin-bottom: 8px; font-weight: 600; }
        .status-info p { color: #4a5b6c; font-size: 13px; }
        .security-notice { margin: 20px 0; padding: 15px; background: #ffebee; border-left: 4px solid #d32f2f; border-radius: 8px; text-align: left; }
        .security-notice h3 { color: #b71c1c; font-size: 14px; margin-bottom: 8px; font-weight: 600; }
        .security-notice p { color: #4a5b6c; font-size: 13px; }
        .footer { background: #f8f9fa; padding: 30px; text-align: center; border-top: 1px solid #e0e4e8; }
        .footer-brand { font-size: 18px; font-weight: 700constexpr
        .footer-brand { font-size: 18px; font-weight: 700; color: #007bff; margin-bottom: 10px; }
        .footer p { color: #6c757d; font-size: 12px; margin-bottom: 8px; }
        .footer a { color: #007bff; text-decoration: none; }
        .footer a:hover { text-decoration: underline; }
        .footer-links { margin-top: 15px; }
        .footer-links a { margin: 0 10px; font-size: 12px; }
        @media (max-width: 600px) {
            .email-container { margin: 10px; border-radius: 8px; }
            .header { padding: 20px; }
            .content { padding: 20px; }
            .header img { width: 80px; }
            .header h1 { font-size: 20滴
            .header img { width: 80px; }
            .header h1 { font-size: 20px; }
            .appointment-details { padding: 10px; }
            .footer { padding: 20px; }
        }
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <img src='https://res.cloudinary.com/dphys6egj/image/upload/v1759812317/Pngtree_hipster_bike_electric_logo_design_4847419_wvci4k.jpg' alt='Logo EV Maintenance'>
            <h1>Thông báo trạng thái bảo dưỡng</h1>
            <p>Cập nhật trạng thái cuộc hẹn của bạn</p>
        </div>
        <div class='content'>
            <div class='greeting'>Xin chào, " + userName + @"!</div>
            <div class='message'>
                Cảm ơn bạn đã sử dụng dịch vụ bảo dưỡng của chúng tôi. Dưới đây là cập nhật về trạng thái cuộc hẹn bảo dưỡng xe của bạn.
            </div>
            <div class='appointment-details'>
                <h3>Thông tin cuộc hẹn</h3>
                <p><strong>Mã cuộc hẹn:</strong> " + appointment.AppointmentId + @"</p>
                <p><strong>Xe:</strong> " + vehicleInfo + @"</p>
                <p><strong>Dịch vụ:</strong> " + serviceNames + @"</p>
                <p><strong>Ngày hẹn:</strong> " + appointmentDate + @"</p>
                <p><strong>Khung giờ:</strong> " + slotTime + @"</p>
                <p><strong>Trung tâm bảo dưỡng:</strong> " + centerName + @"</p>
                <p><strong>Địa chỉ:</strong> " + centerAddress + @"</p>
                <p><strong>Ghi chú:</strong> " + (appointment.Notes ?? "Không có") + @"</p>
            </div>
            <div class='status-info'>
                <h3>Trạng thái cuộc hẹn</h3>
                <p>Cuộc hẹn của bạn hiện đang ở trạng thái: <strong>" + status + @"</strong>.</p>
            </div>
            <div class='security-notice'>
                <h3>Thông báo bảo mật</h3>
                <p>Nếu bạn không thực hiện cuộc hẹn này, vui lòng liên hệ với đội ngũ hỗ trợ của chúng tôi tại <a href='mailto:support@evservicecenter.me'>support@evservicecenter.me</a>.</p>
            </div>
        </div>
        <div class='footer'>
            <div class='footer-brand'>EV Service Center</div>
            <p>Tra Vinh, Viet Nam</p>
            <p>Email: <a href='mailto:support@evservicecenter.me'>support@evservicecenter.me</a> | Điện thoại: 0338302160</p>
            <p>© 2025 EV Service Center. Tất cả quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}