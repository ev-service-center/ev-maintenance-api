namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class RefreshTokenRequestDto
    {
        public string RefreshToken { get; set; } = null!;
    }

    public class RegisterRequestDto
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string FullName { get; set; } = null!;
    }

    public class LoginRequestDto
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    public class ActivateAccountRequestDto
    {
        public int UserId { get; set; }
        public string Token { get; set; } = null!;
    }

    public class CheckOtpPasswordRequestDto
    {
        public string Email { get; set; } = null!;
        public string Otp { get; set; } = null!;
    }
}
