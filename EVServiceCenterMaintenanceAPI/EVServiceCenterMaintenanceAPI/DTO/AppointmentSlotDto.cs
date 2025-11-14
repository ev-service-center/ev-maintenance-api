using System.ComponentModel.DataAnnotations;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class AppointmentSlotResponseDto
    {
        public int SlotId { get; set; }
        public int CenterId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class AppointmentSlotCreateRequestDto
    {
        [Required(ErrorMessage = "CenterId is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "CenterId must be greater than 0.")]
        public int? CenterId { get; set; }

        [Required(ErrorMessage = "StartTime is required.")]
        public DateTime? StartTime { get; set; }

        [Required(ErrorMessage = "EndTime is required.")]
        public DateTime? EndTime { get; set; }

        public bool? IsAvailable { get; set; }
    }

    public class AppointmentSlotUpdateRequestDto
    {
        public bool? IsAvailable { get; set; }
    }
}
