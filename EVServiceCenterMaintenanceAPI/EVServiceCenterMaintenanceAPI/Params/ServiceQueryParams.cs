using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.Params
{
    public class ServiceQueryParams : QueryParams
    {
        public ServiceStatus? StatusService { get; set; }
    }
}
