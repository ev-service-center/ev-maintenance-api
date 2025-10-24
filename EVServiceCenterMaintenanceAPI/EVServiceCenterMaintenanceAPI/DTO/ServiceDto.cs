using System.ComponentModel.DataAnnotations;
using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class ServiceResponseDto
    {
        public int ServiceId { get; set; }
        [Required, StringLength(100)] public string ServiceName { get; set; } = null!;
        public string? Description { get; set; }
        public decimal BasePrice { get; set; }
        public int? EstimatedTime { get; set; }
        public ServiceStatus Status { get; set; }
        public int ReminderIntervalDays { get; set; }
        public decimal ReminderMileage { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
