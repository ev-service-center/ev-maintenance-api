using EVServiceCenterMaintenanceAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class WorkOrderCreateRequestDto
    {
        [Required] public int CenterId { get; set; }
        [Required] public int CustomerId { get; set; }
        [Required] public int VehicleId { get; set; }
        [Required] public int CreatedByStaffId { get; set; }
        public int? AppointmentId { get; set; }
        public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Pending;
        public DateTime? CheckInAt { get; set; }
        public DateTime? CheckOutAt { get; set; }
        public int? OdometerKm { get; set; }
        public string? Notes { get; set; }
        public List<int> ServiceIds { get; set; } = new List<int>();
    }

    public class WorkOrderUpdateRequestDto
    {
        public int? CenterId { get; set; }
        public int? CustomerId { get; set; }
        public int? VehicleId { get; set; }
        public int? CreatedByStaffId { get; set; }
        public int? AppointmentId { get; set; }
        public DateTime? CheckInAt { get; set; }
        public DateTime? CheckOutAt { get; set; }
        [Range(0, int.MaxValue)] public int? OdometerKm { get; set; }
        public string? Notes { get; set; }
    }

    public class WorkOrderUpdateStatusRequestDto
    {
        [Required] public WorkOrderStatus Status { get; set; }
    }

    public class WorkOrderResponseDto
    {
        public int WorkOrderId { get; set; }
        public int CenterId { get; set; }
        public int CustomerId { get; set; }
        public int VehicleId { get; set; }
        public int CreatedByStaffId { get; set; }
        public int? AppointmentId { get; set; }
        public WorkOrderStatus Status { get; set; }
        public DateTime? CheckInAt { get; set; }
        public DateTime? CheckOutAt { get; set; }
        public int? OdometerKm { get; set; }
        public string? Notes { get; set; }
        public string? OrderCode { get; set; }
        public ServiceCenterResponseDto? CenterDetails { get; set; }
        public UserResponseDto? CustomerDetails { get; set; }
        public VehicleResponeDto? VehicleDetails { get; set; }
        public UserResponseDto? CreatedByStaffDetails { get; set; }
        public AppointmentResponseDto? AppointmentDetails { get; set; }

        public List<AppointmentServiceResponseDto>? AppointmentServices { get; set; }
        public List<ServiceWithPartsResponseDto>? ServiceDetails { get; set; }
    }
}

