using System.Text;

namespace EVServiceCenterMaintenanceAPI.Utils
{
    public class OtpHelper
    {
        public static string GenerateOtp()
        {
            const string digits = "0123456789";
            var random = new Random();
            var otp = new StringBuilder(6);
            for (int i = 0; i < 6; i++)
            {
                otp.Append(digits[random.Next(digits.Length)]);
            }
            return otp.ToString();
        }

        public static JwtTokenResult GenerateOtpToken(string otp)
        {
            var expires = DateTime.UtcNow.AddMinutes(5);
            return new JwtTokenResult { Token = otp, ExpiresAt = expires };
        }
    }
}
