using System;
using System.Collections.Generic;

namespace EVServiceCenterMaintenanceAPI.Models;

public partial class AppointmentSlot
{
    public int SlotId { get; set; }

    public int CenterId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public bool IsAvailable { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Appointment? Appointment { get; set; }

    public virtual ServiceCenter Center { get; set; } = null!;
}
