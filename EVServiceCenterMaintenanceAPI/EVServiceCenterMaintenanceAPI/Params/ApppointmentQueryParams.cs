using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.Params
{
    public class AppointmentQueryParams : QueryParams
    {
        public AppointmentStatus? StatusAppointment { get; set; }
        public int? CenterId { get; set; }
        public int? CustomerId { get; set; }
        public int? TechnicianId { get; set; }
    }
}
