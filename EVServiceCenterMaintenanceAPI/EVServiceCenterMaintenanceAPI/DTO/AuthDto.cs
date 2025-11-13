using System.ComponentModel.DataAnnotations;
using EVServiceCenterMaintenanceAPI.Attributes;
using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class RefreshTokenRequestDto
    {
        [Required, StringLength((int)StringLength.Token)]
        public string RefreshToken { get; set; } = null!;
    }

    public class RegisterRequestDto
    {
        [Required, EmailAddress, StringLength((int)StringLength.Email)]
        public string Email { get; set; } = null!;
        [Required, StringLength((int)StringLength.MaxPassWord, MinimumLength = (int)StringLength.MinPassWord)]
        public string Password { get; set; } = null!;
        [Required, StringLength((int)StringLength.FullName)]
        public string FullName { get; set; } = null!;
    }

    public class LoginRequestDto
    {
        [Required, EmailOrUsername, StringLength((int)StringLength.Email)]
        public string EmailOrUsername { get; set; } = null!;
        [Required]
        public string Password { get; set; } = null!;
    }

    public class ActivateAccountRequestDto
    {
        [Required]
        public int? UserId { get; set; }
        [Required, StringLength((int)StringLength.Token)]

        public string Token { get; set; } = null!;
    }

    public class EmailRequestDto
    {
        [Required, EmailAddress, StringLength((int)StringLength.Email)]
        public string Email { get; set; } = null!;
    }

    public class ResetPasswordWithOtpRequestDto
    {
        [Required, EmailAddress, StringLength((int)StringLength.Email)]
        public string Email { get; set; } = null!;
        [Required, StringLength((int)StringLength.Otp)]
        public string Otp { get; set; } = null!;
        [Required, StringLength((int)StringLength.MaxPassWord, MinimumLength = (int)StringLength.MinPassWord)]
        public string Password { get; set; } = null!;
    }

    public class CheckOtpPasswordRequestDto
    {
        [Required, EmailAddress, StringLength((int)StringLength.Email)]
        public string Email { get; set; } = null!;
        [Required, StringLength((int)StringLength.Otp)]
        public string Otp { get; set; } = null!;
    }
}
