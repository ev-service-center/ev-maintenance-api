using EVServiceCenterMaintenanceAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class ReminderResponseDto
    {
        public int ReminderId { get; set; }
        public int UserId { get; set; }
        public int? VehicleId { get; set; }
        public int? ServiceId { get; set; }
        public ReminderType ReminderType { get; set; }
        public DateTime ReminderDate { get; set; }
        public string? Message { get; set; }
        public bool Sent { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ReminderCreateRequestDto
    {
        [Required] public int UserId { get; set; }
        public int? VehicleId { get; set; }
        public int? ServiceId { get; set; }
        [Required] public ReminderType ReminderType { get; set; }
        [Required] public DateTime ReminderDate { get; set; }
        public string? Message { get; set; }
    }
}
