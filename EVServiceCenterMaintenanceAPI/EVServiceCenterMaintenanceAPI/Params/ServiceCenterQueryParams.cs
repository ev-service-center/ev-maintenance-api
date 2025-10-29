using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.Params
{
    public class ServiceCenterQueryParams : QueryParams
    {
        public ServiceCenterStatus? StatusServiceCenter { get; set; }
    }
}
