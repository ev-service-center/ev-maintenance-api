using System.ComponentModel.DataAnnotations;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class MaintenanceHistoryResponseDto
    {
        public int HistoryId { get; set; }
        public int VehicleId { get; set; }
        public int? WorkOrderId { get; set; }
        public int ServiceId { get; set; }
        public DateTime MaintenanceDate { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public decimal? Cost { get; set; }
        public decimal? MileageAtMaintenance { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public ServiceResponseDto? ServiceDetails { get; set; }
        public VehicleResponeDto? VehicleDetails { get; set; }
        public WorkOrderResponseDto? WorkOrderDetails { get; set; }
        public List<PartUsageResponseDto>? PartUsageDetails { get; set; }
    }

    public class MaintenanceHistoryCreateRequestDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "VehicleId must be greater than 0.")]
        public int VehicleId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "ServiceId must be greater than 0.")]
        public int ServiceId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "WorkOrderId must be greater than 0.")]
        public int WorkOrderId { get; set; }

        [Required(ErrorMessage = "MaintenanceDate is required.")]
        public DateTime MaintenanceDate { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
        public string? Notes { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Cost must be greater than or equal to 0.")]
        public decimal? Cost { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Mileage must be greater than or equal to 0.")]
        public decimal? MileageAtMaintenance { get; set; }
    }

    public class MaintenanceHistoryUpdateRequestDto
    {
        [Required] public int HistoryId { get; set; }
        [Required] public int VehicleId { get; set; }
        [Required] public int ServiceId { get; set; }
        public int? WorkOrderId { get; set; }
        [Required] public DateTime MaintenanceDate { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public decimal? Cost { get; set; }
        public decimal? MileageAtMaintenance { get; set; }
    }
}
