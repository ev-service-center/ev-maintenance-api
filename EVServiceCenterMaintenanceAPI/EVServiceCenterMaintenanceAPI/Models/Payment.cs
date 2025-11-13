using System;
using System.Collections.Generic;

namespace EVServiceCenterMaintenanceAPI.Models;

public partial class Payment
{
    public int PaymentId { get; set; }

    public int? InvoiceId { get; set; }

    public int? WorkOrderId { get; set; }

    public DateTime? PaymentDate { get; set; }

    public decimal Amount { get; set; }

    public string Method { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? OrderCode { get; set; }

    public string? TransactionId { get; set; }

    public string? PaymentType { get; set; }

    public virtual Invoice? Invoice { get; set; }

    public virtual WorkOrder? WorkOrder { get; set; }
}
