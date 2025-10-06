using System;
using System.Collections.Generic;

namespace EVServiceCenterMaintenanceAPI.Models;

public partial class PartUsage
{
    public int UsageId { get; set; }

    public int HistoryId { get; set; }

    public int PartId { get; set; }

    public int QuantityUsed { get; set; }

    public virtual MaintenanceHistory History { get; set; } = null!;

    public virtual Part Part { get; set; } = null!;
}
