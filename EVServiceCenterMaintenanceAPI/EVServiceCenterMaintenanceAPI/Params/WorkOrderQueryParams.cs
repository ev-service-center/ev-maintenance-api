namespace EVServiceCenterMaintenanceAPI.Params
{
    public class WorkOrderQueryParams : QueryParams
    {
        public int? CenterId { get; set; }
        public int? CustomerId { get; set; }
        public int? VehicleId { get; set; }
        public string? Status { get; set; }
    }
}
