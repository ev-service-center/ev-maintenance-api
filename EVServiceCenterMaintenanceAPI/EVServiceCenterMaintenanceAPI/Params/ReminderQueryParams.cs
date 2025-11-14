using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.Params
{
    public class ReminderQueryParams : QueryParams
    {
        public ReminderType? ReminderType { get; set; }
        public bool? Sent { get; set; }
        public int? UserId { get; set; }
    }
}
