using System.ComponentModel.DataAnnotations;
using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class EmployeeResponseDto
    {
        public int EmployeeId { get; set; }
        public int CenterId { get; set; }
        public string? Shift { get; set; }
        public decimal PerformanceScore { get; set; }
        public string? Certificate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public EmployeeUserInfoDto? User { get; set; }
        public ServiceCenterResponseDto? Center { get; set; }
    }

    public class EmployeeUserInfoDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public UserRole Role { get; set; }
        public UserStatus Status { get; set; }
        public string? Avatar { get; set; }
    }

    public class EmployeeCreateRequestDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "EmployeeId must be a positive integer.")]
        public int EmployeeId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "CenterId must be a positive integer.")]
        public int CenterId { get; set; }

        [StringLength(50)]
        public string? Shift { get; set; }

        [Range(0, 100, ErrorMessage = "PerformanceScore must be between 0 and 100.")]
        public decimal PerformanceScore { get; set; }

        [StringLength(255)]
        public string? Certificate { get; set; }
    }

    public class EmployeeUpdateRequestDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "CenterId must be a positive integer.")]
        public int? CenterId { get; set; }

        [StringLength(50)]
        public string? Shift { get; set; }

        [Range(0, 100, ErrorMessage = "PerformanceScore must be between 0 and 100.")]
        public decimal? PerformanceScore { get; set; }

        [StringLength(255)]
        public string? Certificate { get; set; }
    }
}
