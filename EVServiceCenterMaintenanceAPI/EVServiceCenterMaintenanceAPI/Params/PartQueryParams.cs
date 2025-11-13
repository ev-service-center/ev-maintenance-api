using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.Params
{
    public class PartQueryParams : QueryParams
    {
        public int? CenterId { get; set; }
        public PartStatus? StatusPart { get; set; }
    }
}