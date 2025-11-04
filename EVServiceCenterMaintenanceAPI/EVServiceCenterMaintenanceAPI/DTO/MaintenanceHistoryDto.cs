using System.ComponentModel.DataAnnotations;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class MaintenanceHistoryResponseDto
    {
        public int HistoryId { get; set; }
        public int VehicleId { get; set; }
        public int? AppointmentId { get; set; }
        public DateTime MaintenanceDate { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public decimal? Cost { get; set; }
        public decimal? MileageAtMaintenance { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class MaintenanceHistoryCreateRequestDto
    {
        [Required] public int VehicleId { get; set; }
        public int? AppointmentId { get; set; }
        [Required] public DateTime MaintenanceDate { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public decimal? Cost { get; set; }
        public decimal? MileageAtMaintenance { get; set; }
    }
}
