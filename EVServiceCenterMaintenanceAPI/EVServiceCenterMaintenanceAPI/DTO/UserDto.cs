using EVServiceCenterMaintenanceAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class UserCreateRequestDto
    {
        [Required, StringLength(50)] public string Username { get; set; } = null!;
        [Required, StringLength(100)] public string FullName { get; set; } = null!;
        [Required, EmailAddress, StringLength(100)] public string Email { get; set; } = null!;
        [StringLength(20)] public string? Phone { get; set; }
        public UserRole Role { get; set; }
        public IFormFile? Avatar { get; set; }
    }

    public class UserResponseDto
    {
        public int UserId { get; set; }
        [Required, StringLength(50)] public string Username { get; set; } = null!;
        [Required, StringLength(100)] public string FullName { get; set; } = null!;
        [Required, EmailAddress, StringLength(100)] public string Email { get; set; } = null!;
        [StringLength(20)] public string? Phone { get; set; }
        public UserRole Role { get; set; }
        public UserStatus Status { get; set; }
        public string? Avatar { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
