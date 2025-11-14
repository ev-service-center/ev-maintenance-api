using System.ComponentModel.DataAnnotations;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class AppointmentServiceResponseDto
    {
        public int AppointmentServiceId { get; set; }
        public int? WorkOrderId { get; set; }
        public int ServiceId { get; set; }
        public decimal Price { get; set; }
        public int? AssignedTechnicianId { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class AppointmentServiceUpdateRequestDto
    {
        public int? ServiceId { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Price must be greater than or equal to 0")]
        public decimal? Price { get; set; }

        public int? AssignedTechnicianId { get; set; }

        public string? Status { get; set; }
    }

    public class AppointmentServiceUpdateStatusRequestDto
    {
        [Required]
        public string Status { get; set; } = null!;
    }

    public class AppointmentServiceAssignTechnicianRequestDto
    {
        [Required]
        public int TechnicianId { get; set; }
    }

}
