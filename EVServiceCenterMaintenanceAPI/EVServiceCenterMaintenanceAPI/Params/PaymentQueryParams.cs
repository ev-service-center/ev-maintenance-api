namespace EVServiceCenterMaintenanceAPI.Params
{
    public class PaymentQueryParams : QueryParams
    {
        public string? PaymentType { get; set; }
        public string? Method { get; set; }
        public int? CustomerId { get; set; }
        public int? WorkOrderId { get; set; }
        public int? InvoiceId { get; set; }
    }
}
