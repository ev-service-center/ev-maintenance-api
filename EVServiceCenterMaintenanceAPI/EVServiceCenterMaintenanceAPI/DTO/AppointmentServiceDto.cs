namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class AppointmentServiceResponseDto
    {
        public int AppointmentServiceId { get; set; }

        public int? WorkOrderId { get; set; }

        public int ServiceId { get; set; }

        public decimal Price { get; set; }
    }
}
