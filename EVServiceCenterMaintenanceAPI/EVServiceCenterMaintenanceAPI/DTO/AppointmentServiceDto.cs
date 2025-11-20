using System.ComponentModel.DataAnnotations;
using EVServiceCenterMaintenanceAPI.Enums;

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
        public ServiceResponseDto? ServiceDetails { get; set; }
        public UserResponseDto? AssignedTechnicianDetails { get; set; }
        public WorkOrderResponseDto? WorkOrderDetails { get; set; }
    }

    public class AppointmentServiceUpdateRequestDto
    {
        public int? ServiceId { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Price must be greater than or equal to 0")]
        public decimal? Price { get; set; }

        public int? AssignedTechnicianId { get; set; }

        public AppointmentServiceStatus? Status { get; set; }
    }

    public class AppointmentServiceUpdateStatusRequestDto
    {
        [Required]
        public AppointmentServiceStatus Status { get; set; }
    }

    public class AppointmentServiceAssignTechnicianRequestDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "TechnicianId must be greater than 0")]
        public int TechnicianId { get; set; }
    }

    public class AppointmentServiceBatchAssignTechnicianRequestDto
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one assignment is required")]
        public List<TechnicianAssignment> Assignments { get; set; } = new List<TechnicianAssignment>();
    }

    public class TechnicianAssignment
    {
        [Required(ErrorMessage = "TechnicianId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "TechnicianId must be greater than 0")]
        public int TechnicianId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one AppointmentServiceId is required")]
        public List<int> AppointmentServiceIds { get; set; } = new List<int>();
    }

    public class AppointmentServiceBatchAssignTechnicianResponseDto
    {
        public int TotalRequested { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public List<AppointmentServiceResponseDto> SuccessItems { get; set; } = new List<AppointmentServiceResponseDto>();
        public List<BatchAssignError> Errors { get; set; } = new List<BatchAssignError>();
    }

    public class BatchAssignError
    {
        public int AppointmentServiceId { get; set; }
        public string ErrorMessage { get; set; } = null!;
    }

}
