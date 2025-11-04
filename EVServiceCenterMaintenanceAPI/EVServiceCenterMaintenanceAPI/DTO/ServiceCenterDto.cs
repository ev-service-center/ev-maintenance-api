using EVServiceCenterMaintenanceAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class ServiceCenterResponseDto
    {
        public int CenterId { get; set; }
        [Required, StringLength(100)] public string CenterName { get; set; } = null!;
        [Required, StringLength(255)] public string Address { get; set; } = null!;
        [StringLength(20)] public string? Phone { get; set; }
        [EmailAddress, StringLength(100)] public string? Email { get; set; }
        public ServiceCenterStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ServiceCenterUpdateRequestDto
    {
        [Required] public int CenterId { get; set; }
        [Required, StringLength(100)] public string CenterName { get; set; } = null!;
        [Required, StringLength(255)] public string Address { get; set; } = null!;
        [StringLength(20)] public string? Phone { get; set; }
        [EmailAddress, StringLength(100)] public string? Email { get; set; }
        public ServiceCenterStatus Status { get; set; }
    }
}
