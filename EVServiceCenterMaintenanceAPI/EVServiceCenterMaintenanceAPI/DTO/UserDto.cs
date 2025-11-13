using EVServiceCenterMaintenanceAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class UserCreateRequestDto
    {
        [Required, StringLength((int)StringLength.MaxUsername, MinimumLength = (int)StringLength.MinUsername)] public string Username { get; set; } = null!;
        [Required, StringLength((int)StringLength.FullName)] public string FullName { get; set; } = null!;
        [Required, EmailAddress, StringLength((int)StringLength.Email)] public string Email { get; set; } = null!;
        [RegularExpression(@"^(\+84|0)[0-9]{9,10}$", ErrorMessage = "Invalid Vietnamese phone number format.")]
        public string? Phone { get; set; }
        [Required(ErrorMessage = "Role is required")]
        public UserRole? Role { get; set; }
        public IFormFile? Avatar { get; set; }
    }

    public class UserUpdateRequestDto
    {
        [StringLength((int)StringLength.FullName)] public string FullName { get; set; } = null!;
        [EmailAddress, StringLength((int)StringLength.Email)] public string Email { get; set; } = null!;
        [RegularExpression(@"^(\+84|0)[0-9]{9,10}$", ErrorMessage = "Invalid Vietnamese phone number format.")]
        public string? Phone { get; set; }
        public UserRole? Role { get; set; }
        public UserStatus? Status { get; set; }
        public IFormFile? Avatar { get; set; }
    }

    public class UserResponseDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public UserRole Role { get; set; }
        public UserStatus Status { get; set; }
        public string? Avatar { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UserProfileUpdateRequestDto
    {
        [StringLength((int)StringLength.MaxUsername)] public string? Username { get; set; }
        [StringLength((int)StringLength.FullName)] public string? FullName { get; set; }
        [EmailAddress, StringLength((int)StringLength.Email)] public string? Email { get; set; }
        [RegularExpression(@"^(\+84|0)[0-9]{9,10}$", ErrorMessage = "Invalid Vietnamese phone number format.")]
        public string? Phone { get; set; }
        public IFormFile? Avatar { get; set; }
    }

    public class ChangePasswordRequestDto
    {
        [Required(ErrorMessage = "Old password is required.")]
        [StringLength((int)StringLength.MaxPassWord, MinimumLength = (int)StringLength.MinPassWord, ErrorMessage = "Old password cannot be empty.")]
        public string OldPassword { get; set; } = null!;

        [Required(ErrorMessage = "New password is required.")]
        [StringLength((int)StringLength.MaxPassWord, MinimumLength = (int)StringLength.MinPassWord, ErrorMessage = "New password must be between 8 and 32 characters.")]
        public string NewPassword { get; set; } = null!;
    }
}
