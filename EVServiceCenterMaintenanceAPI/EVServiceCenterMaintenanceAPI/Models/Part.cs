using System;
using System.Collections.Generic;

namespace EVServiceCenterMaintenanceAPI.Models;

public partial class Part
{
    public int PartId { get; set; }

    public string PartName { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int? QuantityInStock { get; set; }

    public int? MinStock { get; set; }

    public int CenterId { get; set; }

    public string? Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ServiceCenter Center { get; set; } = null!;

    public virtual ICollection<PartUsage> PartUsages { get; set; } = new List<PartUsage>();
}
