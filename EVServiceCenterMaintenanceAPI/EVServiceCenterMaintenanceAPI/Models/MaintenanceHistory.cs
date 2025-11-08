using System;
using System.Collections.Generic;

namespace EVServiceCenterMaintenanceAPI.Models;

public partial class MaintenanceHistory
{
    public int HistoryId { get; set; }

    public int VehicleId { get; set; }

    public int? WorkOrderId { get; set; }

    public int? ServiceId { get; set; }
    public DateTime MaintenanceDate { get; set; }

    public string? Description { get; set; }

    public string? Notes { get; set; }

    public decimal? Cost { get; set; }

    public decimal? MileageAtMaintenance { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<PartUsage> PartUsages { get; set; } = new List<PartUsage>();

    public virtual Service? Service { get; set; }

    public virtual Vehicle Vehicle { get; set; } = null!;

    public virtual WorkOrder? WorkOrder { get; set; }
}
