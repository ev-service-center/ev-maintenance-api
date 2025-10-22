using System;
using System.Collections.Generic;

namespace EVServiceCenterMaintenanceAPI.Models;

public partial class AppointmentService
{
    public int AppointmentServiceId { get; set; }

    public int? WorkOrderId { get; set; }

    public int ServiceId { get; set; }

    public decimal Price { get; set; }

    public int? AssignedTechnicianId { get; set; }

    public string? Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Service Service { get; set; } = null!;

    public virtual WorkOrder? WorkOrder { get; set; }
}
