using System;
using System.Collections.Generic;

namespace EVServiceCenterMaintenanceAPI.Models;

public partial class Employee
{
    public int EmployeeId { get; set; }

    public int CenterId { get; set; }

    public string? Shift { get; set; }

    public decimal? PerformanceScore { get; set; }

    public string? Certificate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ServiceCenter Center { get; set; } = null!;

    public virtual User EmployeeNavigation { get; set; } = null!;
}
