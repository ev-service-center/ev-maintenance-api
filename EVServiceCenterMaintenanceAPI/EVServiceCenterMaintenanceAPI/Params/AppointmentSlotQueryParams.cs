namespace EVServiceCenterMaintenanceAPI.Params
{
    public class AppointmentSlotQueryParams : QueryParams
    {
        public int? CenterId { get; set; }
        public bool? IsAvailable { get; set; }
    }
}
