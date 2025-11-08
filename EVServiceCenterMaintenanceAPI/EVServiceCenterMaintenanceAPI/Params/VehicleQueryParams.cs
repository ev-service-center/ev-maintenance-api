using EVServiceCenterMaintenanceAPI.Params;
using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.Params;

public class VehicleQueryParams : QueryParams
{
    public int? CustomerId { get; set; }
    public VehicleStatus? StatusVehicle { get; set; }
}