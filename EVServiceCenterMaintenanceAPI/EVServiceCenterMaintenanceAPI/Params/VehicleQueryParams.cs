using EVServiceCenterMaintenanceAPI.Params;

namespace EVServiceCenterMaintenanceAPI.Params;

public class VehicleQueryParams : QueryParams
{
    public int? CustomerId { get; set; }
}