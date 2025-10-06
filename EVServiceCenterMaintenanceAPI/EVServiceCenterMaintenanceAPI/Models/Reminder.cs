using System;
using System.Collections.Generic;

namespace EVServiceCenterMaintenanceAPI.Models;

public partial class Reminder
{
    public int ReminderId { get; set; }

    public int UserId { get; set; }

    public int? VehicleId { get; set; }

    public int? ServiceId { get; set; }

    public string? ReminderType { get; set; }

    public DateTime ReminderDate { get; set; }

    public string? Message { get; set; }

    public bool? Sent { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Service? Service { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Vehicle? Vehicle { get; set; }
}
