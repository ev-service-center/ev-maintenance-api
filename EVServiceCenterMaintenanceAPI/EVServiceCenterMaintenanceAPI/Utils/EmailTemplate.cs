using System;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Helpers;
using EVServiceCenterMaintenanceAPI.Models;

namespace EVServiceCenterMaintenanceAPI.Utils
{
    public static class EmailTemplate
    {
        private static DateTime GetVietNamTime(DateTime utcTime)
        {
            return utcTime.ConvertToVietnamTime();
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

        public static string GenerateDepositPaymentConfirmationEmailTemplate(
            string customerName,
            int workOrderId,
            decimal depositAmount,
            string vehicleInfo,
            string appointmentDate,
            string paymentDate,
            string transactionId,
            string orderCode)
        {
            return @"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Xác nhận thanh toán cọc - EV Service Center</title>
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
        .payment-success { background: #d4edda; border: 2px solid #28a745; border-radius: 8px; padding: 20px; margin: 20px 0; }
        .payment-success-icon { font-size: 48px; color: #28a745; margin-bottom: 10px; }
        .payment-success-text { font-size: 18px; font-weight: 600; color: #155724; margin-bottom: 10px; }
        .payment-details { margin: 20px 0; padding: 20px; background: #f8f9fa; border-radius: 8px; text-align: left; }
        .payment-details h3 { color: #1a2b49; font-size: 16px; margin-bottom: 15px; font-weight: 600; }
        /* OLD CSS - Gmail không hỗ trợ flexbox tốt
        .payment-details-row { display: flex; justify-content: space-between; padding: 10px 0; border-bottom: 1px solid #e0e4e8; }
        .payment-details-row:last-child { border-bottom: none; }
        .payment-details-label { color: #6c757d; font-size: 14px; font-weight: 500; }
        .payment-details-value { color: #1a2b49; font-size: 14px; font-weight: 600; }
        */
        /* NEW CSS - Inline-block (tạm thời, có thể rollback)
        .payment-details-row { padding: 10px 0; border-bottom: 1px solid #e0e4e8; }
        .payment-details-row:last-child { border-bottom: none; }
        .payment-details-label { color: #6c757d; font-size: 14px; font-weight: 500; display: inline-block; width: 50%; vertical-align: top; }
        .payment-details-value { color: #1a2b49; font-size: 14px; font-weight: 600; display: inline-block; width: 50%; text-align: right; vertical-align: top; }
        */
        /* NEWEST CSS - Table layout (tốt nhất cho Gmail) */
        .payment-details-table { width: 100%; border-collapse: collapse; }
        .payment-details-row { border-bottom: 1px solid #e0e4e8; }
        .payment-details-row:last-child { border-bottom: none; }
        .payment-details-label { color: #6c757d; font-size: 14px; font-weight: 500; padding: 10px 0; }
        .payment-details-value { color: #1a2b49; font-size: 14px; font-weight: 600; padding: 10px 0; text-align: right; }
        .amount-highlight { font-size: 24px; color: #007bff; font-weight: 700; }
        .appointment-info { margin: 20px 0; padding: 15px; background: #e3f2fd; border-left: 4px solid #2196f3; border-radius: 8px; text-align: left; }
        .appointment-info h3 { color: #1565c0; font-size: 14px; margin-bottom: 8px; font-weight: 600; }
        .appointment-info p { color: #4a5b6c; font-size: 13px; margin-bottom: 5px; }
        .next-steps { margin-top: 20px; padding: 15px; background: #fff8e1; border-left: 4px solid #ffca28; border-radius: 8px; text-align: left; }
        .next-steps h3 { color: #e65100; font-size: 14px; margin-bottom: 8px; font-weight: 600; }
        .next-steps p { color: #4a5b6c; font-size: 13px; }
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
            /* OLD CSS - Comment lại
            .payment-details-row { flex-direction: column; }
            .payment-details-value { margin-top: 5px; }
            */
            /* NEW CSS - Inline-block (comment lại)
            .payment-details-label { width: 100%; display: block; margin-bottom: 5px; }
            .payment-details-value { width: 100%; display: block; text-align: left; margin-top: 5px; }
            */
            /* NEWEST CSS - Table layout cho mobile */
            .payment-details-table, .total-amount-table { width: 100% !important; }
            .payment-details-label, .payment-details-value, .total-amount-label, .total-amount-value { 
                display: block !important; 
                width: 100% !important; 
                text-align: left !important; 
                padding: 5px 0 !important; 
            }
        }
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <img src='https://res.cloudinary.com/dphys6egj/image/upload/v1759812317/Pngtree_hipster_bike_electric_logo_design_4847419_wvci4k.jpg' alt='Logo EV Service Center'>
            <h1>Xác nhận thanh toán cọc</h1>
            <p>Thanh toán của bạn đã được xác nhận</p>
        </div>
        <div class='content'>
            <div class='greeting'>Xin chào, " + customerName + @"!</div>
            <div class='message'>
                Cảm ơn bạn đã thanh toán cọc cho dịch vụ bảo dưỡng. Chúng tôi đã nhận được thanh toán của bạn và cuộc hẹn đã được xác nhận.
            </div>
            <div class='payment-success'>
                <div class='payment-success-icon'>✓</div>
                <div class='payment-success-text'>Thanh toán thành công!</div>
            </div>
            <div class='payment-details'>
                <h3>Chi tiết thanh toán</h3>
                <!-- OLD HTML - Div layout (comment để rollback)
                <div class='payment-details-row'>
                    <span class='payment-details-label'>Số tiền đã thanh toán:</span>
                    <span class='payment-details-value amount-highlight'>" + depositAmount.ToString("N0") + @" VNĐ</span>
                </div>
                <div class='payment-details-row'>
                    <span class='payment-details-label'>Mã đơn hàng:</span>
                    <span class='payment-details-value'>" + workOrderId.ToString("D6") + @"</span>
                </div>
                <div class='payment-details-row'>
                    <span class='payment-details-label'>Mã giao dịch:</span>
                    <span class='payment-details-value'>" + transactionId + @"</span>
                </div>
                <div class='payment-details-row'>
                    <span class='payment-details-label'>Mã đơn hàng PayOS:</span>
                    <span class='payment-details-value'>" + orderCode + @"</span>
                </div>
                <div class='payment-details-row'>
                    <span class='payment-details-label'>Ngày thanh toán:</span>
                    <span class='payment-details-value'>" + paymentDate + @"</span>
                </div>
                <div class='payment-details-row'>
                    <span class='payment-details-label'>Phương thức:</span>
                    <span class='payment-details-value'>PayOS</span>
                </div>
                -->
                <!-- NEW HTML - Table layout (tốt nhất cho Gmail) -->
                <table class='payment-details-table'>
                    <tr class='payment-details-row'>
                        <td class='payment-details-label'>Số tiền đã thanh toán:</td>
                        <td class='payment-details-value amount-highlight'>" + depositAmount.ToString("N0") + @" VNĐ</td>
                    </tr>
                    <tr class='payment-details-row'>
                        <td class='payment-details-label'>Mã đơn hàng:</td>
                        <td class='payment-details-value'>" + workOrderId.ToString("D6") + @"</td>
                    </tr>
                    <tr class='payment-details-row'>
                        <td class='payment-details-label'>Mã giao dịch:</td>
                        <td class='payment-details-value'>" + transactionId + @"</td>
                    </tr>
                    <tr class='payment-details-row'>
                        <td class='payment-details-label'>Mã đơn hàng PayOS:</td>
                        <td class='payment-details-value'>" + orderCode + @"</td>
                    </tr>
                    <tr class='payment-details-row'>
                        <td class='payment-details-label'>Ngày thanh toán:</td>
                        <td class='payment-details-value'>" + paymentDate + @"</td>
                    </tr>
                    <tr class='payment-details-row'>
                        <td class='payment-details-label'>Phương thức:</td>
                        <td class='payment-details-value'>PayOS</td>
                    </tr>
                </table>
            </div>
            <div class='appointment-info'>
                <h3>Thông tin cuộc hẹn</h3>
                <p><strong>Xe:</strong> " + vehicleInfo + @"</p>
                <p><strong>Ngày hẹn:</strong> " + appointmentDate + @"</p>
            </div>
            <div class='next-steps'>
                <h3>Bước tiếp theo</h3>
                <p>Cuộc hẹn của bạn đã được xác nhận. Vui lòng đến đúng giờ hẹn tại trung tâm bảo dưỡng. Sau khi hoàn tất bảo dưỡng, bạn sẽ thanh toán phần còn lại.</p>
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
        public static string GenerateFinalPaymentConfirmationEmailTemplate(
            string customerName,
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
            string remainingAmount = (totalAmount - totalPaid).ToString("N0");
            string statusText = invoiceStatus == "Paid" ? "Đã thanh toán đầy đủ" : "Đã thanh toán một phần";
            string statusColor = invoiceStatus == "Paid" ? "#28a745" : "#ff9800";

            return @"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Xác nhận thanh toán cuối - EV Service Center</title>
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
        .payment-success { background: #d4edda; border: 2px solid #28a745; border-radius: 8px; padding: 20px; margin: 20px 0; }
        .payment-success-icon { font-size: 48px; color: #28a745; margin-bottom: 10px; }
        .payment-success-text { font-size: 18px; font-weight: 600; color: #155724; margin-bottom: 10px; }
        .invoice-status { display: inline-block; padding: 8px 16px; border-radius: 20px; font-size: 14px; font-weight: 600; margin: 10px 0; background: " + statusColor + @"; color: #ffffff; }
        .payment-details { margin: 20px 0; padding: 20px; background: #f8f9fa; border-radius: 8px; text-align: left; }
        .payment-details h3 { color: #1a2b49; font-size: 16px; margin-bottom: 15px; font-weight: 600; }
        /* OLD CSS - Gmail không hỗ trợ flexbox tốt, comment lại để rollback nếu cần
        .payment-details-row { display: flex; justify-content: space-between; padding: 10px 0; border-bottom: 1px solid #e0e4e8; }
        .payment-details-row:last-child { border-bottom: none; }
        .payment-details-label { color: #6c757d; font-size: 14px; font-weight: 500; }
        .payment-details-value { color: #1a2b49; font-size: 14px; font-weight: 600; }
        */
        /* NEW CSS - Inline-block (tạm thời, có thể rollback)
        .payment-details-row { padding: 10px 0; border-bottom: 1px solid #e0e4e8; }
        .payment-details-row:last-child { border-bottom: none; }
        .payment-details-label { color: #6c757d; font-size: 14px; font-weight: 500; display: inline-block; width: 50%; vertical-align: top; }
        .payment-details-value { color: #1a2b49; font-size: 14px; font-weight: 600; display: inline-block; width: 50%; text-align: right; vertical-align: top; }
        */
        /* NEWEST CSS - Table layout (tốt nhất cho Gmail) */
        .payment-details-table { width: 100%; border-collapse: collapse; }
        .payment-details-row { border-bottom: 1px solid #e0e4e8; }
        .payment-details-row:last-child { border-bottom: none; }
        .payment-details-label { color: #6c757d; font-size: 14px; font-weight: 500; padding: 10px 0; }
        .payment-details-value { color: #1a2b49; font-size: 14px; font-weight: 600; padding: 10px 0; text-align: right; }
        .amount-highlight { font-size: 24px; color: #28a745; font-weight: 700; }
        .total-amount { background: #e3f2fd; padding: 15px; border-radius: 8px; margin: 15px 0; }
        /* OLD CSS - Comment lại
        .total-amount-row { display: flex; justify-content: space-between; padding: 8px 0; }
        */
        /* NEW CSS - Inline-block (comment lại)
        .total-amount-row { padding: 8px 0; }
        .total-amount-label { font-size: 16px; font-weight: 600; color: #1a2b49; display: inline-block; width: 50%; vertical-align: top; }
        .total-amount-value { font-size: 20px; font-weight: 700; color: #007bff; display: inline-block; width: 50%; text-align: right; vertical-align: top; }
        */
        /* NEWEST CSS - Table layout cho total-amount */
        .total-amount-table { width: 100%; border-collapse: collapse; }
        .total-amount-row { border-bottom: 1px solid #e0e4e8; }
        .total-amount-row:last-child { border-bottom: none; }
        .total-amount-label { font-size: 16px; font-weight: 600; color: #1a2b49; padding: 8px 0; }
        .total-amount-value { font-size: 20px; font-weight: 700; color: #007bff; padding: 8px 0; text-align: right; }
        .summary-info { margin: 20px 0; padding: 15px; background: #e3f2fd; border-left: 4px solid #2196f3; border-radius: 8px; text-align: left; }
        .summary-info h3 { color: #1565c0; font-size: 14px; margin-bottom: 8px; font-weight: 600; }
        .summary-info p { color: #4a5b6c; font-size: 13px; margin-bottom: 5px; }
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
            /* OLD CSS - Comment lại
            .payment-details-row { flex-direction: column; }
            .payment-details-value { margin-top: 5px; }
            */
            /* NEW CSS - Inline-block (comment lại)
            .payment-details-label { width: 100%; display: block; margin-bottom: 5px; }
            .payment-details-value { width: 100%; display: block; text-align: left; margin-top: 5px; }
            */
            /* NEWEST CSS - Table layout cho mobile */
            .payment-details-table, .total-amount-table { width: 100% !important; }
            .payment-details-label, .payment-details-value, .total-amount-label, .total-amount-value { 
                display: block !important; 
                width: 100% !important; 
                text-align: left !important; 
                padding: 5px 0 !important; 
            }
        }
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <img src='https://res.cloudinary.com/dphys6egj/image/upload/v1759812317/Pngtree_hipster_bike_electric_logo_design_4847419_wvci4k.jpg' alt='Logo EV Service Center'>
            <h1>Xác nhận thanh toán cuối</h1>
            <p>Thanh toán của bạn đã được xác nhận</p>
        </div>
        <div class='content'>
            <div class='greeting'>Xin chào, " + customerName + @"!</div>
            <div class='message'>
                Cảm ơn bạn đã thanh toán cho dịch vụ bảo dưỡng. Chúng tôi đã nhận được thanh toán của bạn.
            </div>
            <div class='payment-success'>
                <div class='payment-success-icon'>✓</div>
                <div class='payment-success-text'>Thanh toán thành công!</div>
                <div class='invoice-status'>" + statusText + @"</div>
            </div>
            <div class='payment-details'>
                <h3>Chi tiết thanh toán lần này</h3>
                <!-- OLD HTML - Div layout (comment để rollback)
                <div class='payment-details-row'>
                    <span class='payment-details-label'>Số tiền thanh toán:</span>
                    <span class='payment-details-value amount-highlight'>" + finalAmount.ToString("N0") + @" VNĐ</span>
                </div>
                <div class='payment-details-row'>
                    <span class='payment-details-label'>Mã hóa đơn:</span>
                    <span class='payment-details-value'>" + invoiceId.ToString("D6") + @"</span>
                </div>
                <div class='payment-details-row'>
                    <span class='payment-details-label'>Mã đơn hàng:</span>
                    <span class='payment-details-value'>" + workOrderId.ToString("D6") + @"</span>
                </div>
                <div class='payment-details-row'>
                    <span class='payment-details-label'>Mã giao dịch:</span>
                    <span class='payment-details-value'>" + transactionId + @"</span>
                </div>
                <div class='payment-details-row'>
                    <span class='payment-details-label'>Mã đơn hàng PayOS:</span>
                    <span class='payment-details-value'>" + orderCode + @"</span>
                </div>
                <div class='payment-details-row'>
                    <span class='payment-details-label'>Ngày thanh toán:</span>
                    <span class='payment-details-value'>" + paymentDate + @"</span>
                </div>
                <div class='payment-details-row'>
                    <span class='payment-details-label'>Phương thức:</span>
                    <span class='payment-details-value'>PayOS</span>
                </div>
                -->
                <!-- NEW HTML - Table layout (tốt nhất cho Gmail) -->
                <table class='payment-details-table'>
                    <tr class='payment-details-row'>
                        <td class='payment-details-label'>Số tiền thanh toán:</td>
                        <td class='payment-details-value amount-highlight'>" + finalAmount.ToString("N0") + @" VNĐ</td>
                    </tr>
                    <tr class='payment-details-row'>
                        <td class='payment-details-label'>Mã hóa đơn:</td>
                        <td class='payment-details-value'>" + invoiceId.ToString("D6") + @"</td>
                    </tr>
                    <tr class='payment-details-row'>
                        <td class='payment-details-label'>Mã đơn hàng:</td>
                        <td class='payment-details-value'>" + workOrderId.ToString("D6") + @"</td>
                    </tr>
                    <tr class='payment-details-row'>
                        <td class='payment-details-label'>Mã giao dịch:</td>
                        <td class='payment-details-value'>" + transactionId + @"</td>
                    </tr>
                    <tr class='payment-details-row'>
                        <td class='payment-details-label'>Mã đơn hàng PayOS:</td>
                        <td class='payment-details-value'>" + orderCode + @"</td>
                    </tr>
                    <tr class='payment-details-row'>
                        <td class='payment-details-label'>Ngày thanh toán:</td>
                        <td class='payment-details-value'>" + paymentDate + @"</td>
                    </tr>
                    <tr class='payment-details-row'>
                        <td class='payment-details-label'>Phương thức:</td>
                        <td class='payment-details-value'>PayOS</td>
                    </tr>
                </table>
            </div>
            <div class='total-amount'>
                <!-- OLD HTML - Div layout (comment để rollback)
                <div class='total-amount-row'>
                    <span class='total-amount-label'>Tổng tiền hóa đơn:</span>
                    <span class='total-amount-value'>" + totalAmount.ToString("N0") + @" VNĐ</span>
                </div>
                <div class='total-amount-row'>
                    <span class='total-amount-label'>Đã thanh toán:</span>
                    <span class='total-amount-value'>" + totalPaid.ToString("N0") + @" VNĐ</span>
                </div>" +
                (invoiceStatus == "PartiallyPaid" ? @"
                <div class='total-amount-row'>
                    <span class='total-amount-label'>Còn lại:</span>
                    <span class='total-amount-value' style='color: #ff9800;'>" + remainingAmount + @" VNĐ</span>
                </div>" : "") + @"
                -->
                <!-- NEW HTML - Table layout (tốt nhất cho Gmail) -->
                <table class='total-amount-table'>
                    <tr class='total-amount-row'>
                        <td class='total-amount-label'>Tổng tiền hóa đơn:</td>
                        <td class='total-amount-value'>" + totalAmount.ToString("N0") + @" VNĐ</td>
                    </tr>
                    <tr class='total-amount-row'>
                        <td class='total-amount-label'>Đã thanh toán:</td>
                        <td class='total-amount-value'>" + totalPaid.ToString("N0") + @" VNĐ</td>
                    </tr>" +
                    (invoiceStatus == "PartiallyPaid" ? @"
                    <tr class='total-amount-row'>
                        <td class='total-amount-label'>Còn lại:</td>
                        <td class='total-amount-value' style='color: #ff9800;'>" + remainingAmount + @" VNĐ</td>
                    </tr>" : "") + @"
                </table>
            </div>
            <div class='summary-info'>
                <h3>Thông tin dịch vụ</h3>
                <p><strong>Xe:</strong> " + vehicleInfo + @"</p>
                <p><strong>Trạng thái hóa đơn:</strong> " + statusText + @"</p>
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

        public static string GenerateMaintenanceHistoryEmailTemplate(MaintenanceHistoryResponseDto history)
        {
            string userName = history.WorkOrderDetails?.CustomerDetails?.FullName ?? "Khách hàng";
            string vehicleInfo = $"{history.VehicleDetails?.Model ?? "N/A"} ({history.VehicleDetails?.Plate ?? "N/A"})";
            string maintenanceDate = history.MaintenanceDate.ToString("dd/MM/yyyy");
            string description = string.IsNullOrWhiteSpace(history.Description) ? "Không có mô tả" : history.Description!;
            string notes = string.IsNullOrWhiteSpace(history.Notes) ? "Không có ghi chú" : history.Notes!;
            string cost = history.Cost.HasValue ? history.Cost.Value.ToString("N0") + " VNĐ" : "N/A";
            string mileage = history.MileageAtMaintenance.HasValue ? history.MileageAtMaintenance.Value.ToString("N0") + " km" : "N/A";
            string workOrderId = history.WorkOrderId?.ToString() ?? "N/A";
            string serviceNames = history.WorkOrderDetails?.ServiceDetails != null && history.WorkOrderDetails.ServiceDetails.Any()
                ? string.Join(", ", history.WorkOrderDetails.ServiceDetails.Select(s => s.ServiceName))
                : "Không có dịch vụ";
            string centerName = history.WorkOrderDetails?.CenterDetails?.CenterName ?? "N/A";
            string centerAddress = history.WorkOrderDetails?.CenterDetails?.Address ?? "N/A";
            string partUsages = history.PartUsageDetails != null && history.PartUsageDetails.Count != 0
                ? string.Join("<br>", history.PartUsageDetails.Select(pu =>
                    $"{pu.QuantityUsed} x {(string.IsNullOrWhiteSpace(pu.PartName) ? ("Mã linh kiện: " + pu.PartId) : pu.PartName)} (Đơn giá: {pu.UnitPrice:N0} VNĐ, Tổng: {pu.TotalPrice:N0} VNĐ)"))
                : "Không có linh kiện sử dụng";

            return @"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Thông báo lịch sử bảo dưỡng xe</title>
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
        .maintenance-details { margin: 20px 0; padding: 15px; background: #f8f9fa; border-radius: 8px; text-align: left; }
        .maintenance-details h3 { color: #1a2b49; font-size: 16px; margin-bottom: 10px; font-weight: 600; }
        .maintenance-details p { color: #4a5b6c; font-size: 14px; margin-bottom: 8px; }
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
            .maintenance-details { padding: 10px; }
            .footer { padding: 20px; }
        }
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <img src='https://res.cloudinary.com/dphys6egj/image/upload/v1759812317/Pngtree_hipster_bike_electric_logo_design_4847419_wvci4k.jpg' alt='Logo EV Service Center'>
            <h1>Thông báo lịch sử bảo dưỡng</h1>
            <p>Thông tin lịch sử bảo dưỡng xe của bạn</p>
        </div>
        <div class='content'>
            <div class='greeting'>Xin chào, " + userName + @"!</div>
            <div class='message'>
                Cảm ơn bạn đã sử dụng dịch vụ bảo dưỡng của chúng tôi. Dưới đây là thông tin về lịch sử bảo dưỡng xe của bạn.
            </div>
            <div class='maintenance-details'>
                <h3>Thông tin bảo dưỡng</h3>
                <p><strong>Mã lịch sử bảo dưỡng:</strong> " + history.HistoryId + @"</p>
                <p><strong>Mã lệnh công việc:</strong> " + workOrderId + @"</p>
                <p><strong>Xe:</strong> " + vehicleInfo + @"</p>
                <p><strong>Dịch vụ:</strong> " + serviceNames + @"</p>
                <p><strong>Ngày bảo dưỡng:</strong> " + maintenanceDate + @"</p>
                <p><strong>Trung tâm bảo dưỡng:</strong> " + centerName + @"</p>
                <p><strong>Địa chỉ:</strong> " + centerAddress + @"</p>
                <p><strong>Chi phí:</strong> " + cost + @"</p>
                <p><strong>Số km tại thời điểm bảo dưỡng:</strong> " + mileage + @"</p>
                <p><strong>Mô tả:</strong> " + description + @"</p>
                <p><strong>Ghi chú:</strong> " + notes + @"</p>
                <p><strong>Linh kiện sử dụng:</strong> " + partUsages + @"</p>
            </div>
            <div class='security-notice'>
                <h3>Thông báo bảo mật</h3>
                <p>Nếu bạn không thực hiện bảo dưỡng này, vui lòng liên hệ với đội ngũ hỗ trợ của chúng tôi tại <a href='mailto:support@evservicecenter.me'>support@evservicecenter.me</a>.</p>
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

        public static string GenerateMaintenanceHistoriesEmailTemplate(List<MaintenanceHistoryResponseDto> histories)
        {
            if (histories == null || histories.Count == 0)
            {
                return GenerateMaintenanceHistoryEmailTemplate(new MaintenanceHistoryResponseDto());
            }

            var firstHistory = histories.First();
            string userName = firstHistory.WorkOrderDetails?.CustomerDetails?.FullName ?? "Khách hàng";
            string vehicleInfo = $"{firstHistory.VehicleDetails?.Model ?? "N/A"} ({firstHistory.VehicleDetails?.Plate ?? "N/A"})";
            string workOrderId = firstHistory.WorkOrderId?.ToString() ?? "N/A";
            string centerName = firstHistory.WorkOrderDetails?.CenterDetails?.CenterName ?? "N/A";
            string centerAddress = firstHistory.WorkOrderDetails?.CenterDetails?.Address ?? "N/A";

            // Build services list
            var allServiceNames = histories
                .Where(h => h.ServiceDetails != null)
                .Select(h => h.ServiceDetails!.ServiceName)
                .Distinct()
                .ToList();
            string serviceNames = allServiceNames.Any()
                ? string.Join(", ", allServiceNames)
                : "Không có dịch vụ";

            // Calculate totals
            decimal totalCost = histories.Where(h => h.Cost.HasValue).Sum(h => h.Cost!.Value);
            var allPartUsages = histories
                .Where(h => h.PartUsageDetails != null && h.PartUsageDetails.Any())
                .SelectMany(h => h.PartUsageDetails!)
                .ToList();
            decimal totalPartCost = allPartUsages.Sum(pu => pu.TotalPrice);

            // Build histories HTML
            string historiesHtml = string.Join("", histories.Select((history, index) =>
            {
                string maintenanceDate = history.MaintenanceDate.ToString("dd/MM/yyyy");
                string description = string.IsNullOrWhiteSpace(history.Description) ? "Không có mô tả" : history.Description!;
                string notes = string.IsNullOrWhiteSpace(history.Notes) ? "Không có ghi chú" : history.Notes!;
                string cost = history.Cost.HasValue ? history.Cost.Value.ToString("N0") + " VNĐ" : "N/A";
                string mileage = history.MileageAtMaintenance.HasValue ? history.MileageAtMaintenance.Value.ToString("N0") + " km" : "N/A";
                string serviceName = history.ServiceDetails?.ServiceName ?? "N/A";
                string partUsages = history.PartUsageDetails != null && history.PartUsageDetails.Count != 0
                    ? string.Join("<br>", history.PartUsageDetails.Select(pu =>
                        $"{pu.QuantityUsed} x {(string.IsNullOrWhiteSpace(pu.PartName) ? ("Mã linh kiện: " + pu.PartId) : pu.PartName)} (Đơn giá: {pu.UnitPrice:N0} VNĐ, Tổng: {pu.TotalPrice:N0} VNĐ)"))
                    : "Không có linh kiện sử dụng";

                return $@"
            <div class='maintenance-item' style='margin-bottom: 25px; padding: 20px; background: #f8f9fa; border-radius: 8px; border-left: 4px solid #007bff;'>
                <h4 style='color: #1a2b49; font-size: 16px; margin-bottom: 15px; font-weight: 600;'>Dịch vụ {index + 1}: {serviceName}</h4>
                <p style='color: #4a5b6c; font-size: 14px; margin-bottom: 8px;'><strong>Mã lịch sử bảo dưỡng:</strong> {history.HistoryId}</p>
                <p style='color: #4a5b6c; font-size: 14px; margin-bottom: 8px;'><strong>Ngày bảo dưỡng:</strong> {maintenanceDate}</p>
                <p style='color: #4a5b6c; font-size: 14px; margin-bottom: 8px;'><strong>Chi phí:</strong> {cost}</p>
                <p style='color: #4a5b6c; font-size: 14px; margin-bottom: 8px;'><strong>Số km tại thời điểm bảo dưỡng:</strong> {mileage}</p>
                <p style='color: #4a5b6c; font-size: 14px; margin-bottom: 8px;'><strong>Mô tả:</strong> {description}</p>
                <p style='color: #4a5b6c; font-size: 14px; margin-bottom: 8px;'><strong>Ghi chú:</strong> {notes}</p>
                <p style='color: #4a5b6c; font-size: 14px; margin-bottom: 8px;'><strong>Linh kiện sử dụng:</strong> {partUsages}</p>
            </div>";
            }));

            return $@"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Thông báo lịch sử bảo dưỡng xe</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; background: #f4f7fa; padding: 20px; }}
        .email-container {{ max-width: 600px; margin: 0 auto; background: #ffffff; border-radius: 12px; box-shadow: 0 8px 24px rgba(0, 0, 0, 0.1); overflow: hidden; }}
        .header {{ background: linear-gradient(135deg, #007bff, #00c4cc); padding: 30px; text-align: center; position: relative; }}
        .header img {{ width: 100px; height: auto; margin-bottom: 15px; }}
        .header h1 {{ color: #ffffff; font-size: 24px; font-weight: 600; margin-bottom: 10px; }}
        .header p {{ color: rgba(255, 255, 255, 0.9); font-size: 14px; }}
        .content {{ padding: 40px; text-align: center; }}
        .greeting {{ font-size: 18px; color: #1a2b49; margin-bottom: 20px; font-weight: 600; }}
        .message {{ font-size: 15px; color: #4a5b6c; margin-bottom: 30px; line-height: 1.7; }}
        .summary-details {{ margin: 20px 0; padding: 15px; background: #e3f2fd; border-radius: 8px; text-align: left; }}
        .summary-details h3 {{ color: #1a2b49; font-size: 16px; margin-bottom: 10px; font-weight: 600; }}
        .summary-details p {{ color: #4a5b6c; font-size: 14px; margin-bottom: 8px; }}
        .maintenance-item {{ text-align: left; }}
        .security-notice {{ margin: 20px 0; padding: 15px; background: #ffebee; border-left: 4px solid #d32f2f; border-radius: 8px; text-align: left; }}
        .security-notice h3 {{ color: #b71c1c; font-size: 14px; margin-bottom: 8px; font-weight: 600; }}
        .security-notice p {{ color: #4a5b6c; font-size: 13px; }}
        .footer {{ background: #f8f9fa; padding: 30px; text-align: center; border-top: 1px solid #e0e4e8; }}
        .footer-brand {{ font-size: 18px; font-weight: 700; color: #007bff; margin-bottom: 10px; }}
        .footer p {{ color: #6c757d; font-size: 12px; margin-bottom: 8px; }}
        .footer a {{ color: #007bff; text-decoration: none; }}
        .footer a:hover {{ text-decoration: underline; }}
        @media (max-width: 600px) {{
            .email-container {{ margin: 10px; border-radius: 8px; }}
            .header {{ padding: 20px; }}
            .content {{ padding: 20px; }}
            .header img {{ width: 80px; }}
            .header h1 {{ font-size: 20px; }}
            .summary-details {{ padding: 10px; }}
            .footer {{ padding: 20px; }}
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <img src='https://res.cloudinary.com/dphys6egj/image/upload/v1759812317/Pngtree_hipster_bike_electric_logo_design_4847419_wvci4k.jpg' alt='Logo EV Service Center'>
            <h1>Thông báo lịch sử bảo dưỡng</h1>
            <p>Thông tin lịch sử bảo dưỡng xe của bạn</p>
        </div>
        <div class='content'>
            <div class='greeting'>Xin chào, {userName}!</div>
            <div class='message'>
                Cảm ơn bạn đã sử dụng dịch vụ bảo dưỡng của chúng tôi. Dưới đây là thông tin về tất cả các dịch vụ bảo dưỡng đã hoàn thành cho xe của bạn.
            </div>
            <div class='summary-details'>
                <h3>Thông tin tổng quan</h3>
                <p><strong>Mã lệnh công việc:</strong> {workOrderId}</p>
                <p><strong>Xe:</strong> {vehicleInfo}</p>
                <p><strong>Dịch vụ đã thực hiện:</strong> {serviceNames}</p>
                <p><strong>Trung tâm bảo dưỡng:</strong> {centerName}</p>
                <p><strong>Địa chỉ:</strong> {centerAddress}</p>
                <p><strong>Tổng số dịch vụ:</strong> {histories.Count}</p>
                <p><strong>Tổng chi phí dịch vụ:</strong> {totalCost:N0} VNĐ</p>
                <p><strong>Tổng chi phí linh kiện:</strong> {totalPartCost:N0} VNĐ</p>
                <p><strong>Tổng cộng:</strong> {totalCost + totalPartCost:N0} VNĐ</p>
            </div>
            <div style='margin: 30px 0;'>
                <h3 style='color: #1a2b49; font-size: 18px; margin-bottom: 20px; font-weight: 600; text-align: left;'>Chi tiết từng dịch vụ</h3>
                {historiesHtml}
            </div>
            <div class='security-notice'>
                <h3>Thông báo bảo mật</h3>
                <p>Nếu bạn không thực hiện các bảo dưỡng này, vui lòng liên hệ với đội ngũ hỗ trợ của chúng tôi tại <a href='mailto:support@evservicecenter.me'>support@evservicecenter.me</a>.</p>
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