using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.Params
{
    public class InvoiceQueryParams : QueryParams
    {
        public InvoiceStatus? Status { get; set; }
        public int? CustomerId { get; set; }
        public int? WorkOrderId { get; set; }
    }
}

