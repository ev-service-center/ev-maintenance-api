using EVServiceCenterMaintenanceAPI.Enums;
using System.ComponentModel.DataAnnotations;
namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class AppointmentResponseDto
    {
        public int AppointmentId { get; set; }
        public int CustomerId { get; set; }
        public int VehicleId { get; set; }
        public int CenterId { get; set; }
        public int SlotId { get; set; }
        public DateTime AppointmentDate { get; set; }
        public AppointmentStatus Status { get; set; }
        public string? Notes { get; set; }
        public int? AssignedTechnicianId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public decimal? Amount { get; set; }
        public string? PaymentLink { get; set; }
        public decimal? DepositAmount { get; set; }
        public decimal? RemainingAmount { get; set; }

        public VehicleResponeDto? VehicleDetails { get; set; }
        public UserResponseDto? CustomerDetails { get; set; }
        public ServiceCenterResponseDto? CenterDetails { get; set; }
        public AppointmentSlotResponseDto? SlotDetails { get; set; }
        public WorkOrderResponseDto? WorkOrderDetails { get; set; }
    }
    public class AppointmentCreateRequestDto
    {
        [Required] public int CustomerId { get; set; }
        [Required] public int VehicleId { get; set; }
        [Required] public int CenterId { get; set; }
        [Required] public int SlotId { get; set; }
        public string? Notes { get; set; }
        public List<int> ServiceIds { get; set; } = new List<int>();
    }
    public class AppointmentBookingRequestDto
    {
        [Required] public int CustomerId { get; set; }
        [Required] public int VehicleId { get; set; }
        [Required] public int CenterId { get; set; }
        [Required] public int SlotId { get; set; }
        public string? Notes { get; set; }
        public string? PaymentMethod { get; set; }
        public List<int> ServiceIds { get; set; } = new List<int>();
    }
    public class AppointmentUpdateRequestDto
    {
        public int? CustomerId { get; set; }
        public int? VehicleId { get; set; }
        public int? CenterId { get; set; }
        public int? SlotId { get; set; }
        public DateTime? AppointmentDate { get; set; }
        public AppointmentStatus? Status { get; set; }
        public string? Notes { get; set; }
        public int? AssignedTechnicianId { get; set; }
    }
    public class AppointmentUpdateStatusRequestDto
    {
        public AppointmentStatus Status { get; set; }
    }
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
        public ServiceCenterResponseDto? CenterDetails { get; set; }
        public UserResponseDto? CustomerDetails { get; set; }
        public VehicleResponeDto? VehicleDetails { get; set; }
        public UserResponseDto? CreatedByStaffDetails { get; set; }
        public AppointmentResponseDto? AppointmentDetails { get; set; }

        public List<AppointmentServiceResponseDto>? AppointmentServices { get; set; }
        public List<ServiceWithPartsResponseDto>? ServiceDetails { get; set; }
    }
}