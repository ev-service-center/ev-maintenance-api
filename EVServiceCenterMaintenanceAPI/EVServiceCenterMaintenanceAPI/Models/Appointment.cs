using System;
using System.Collections.Generic;

namespace EVServiceCenterMaintenanceAPI.Models;

public partial class Appointment
{
    public int AppointmentId { get; set; }

    public int CustomerId { get; set; }

    public int VehicleId { get; set; }

    public int CenterId { get; set; }

    public int ServiceId { get; set; }

    public int SlotId { get; set; }

    public DateTime AppointmentDate { get; set; }

    public string? Status { get; set; }

    public string? Notes { get; set; }

    public int? AssignedTechnicianId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User? AssignedTechnician { get; set; }

    public virtual ServiceCenter Center { get; set; } = null!;

    public virtual User Customer { get; set; } = null!;

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<MaintenanceHistory> MaintenanceHistories { get; set; } = new List<MaintenanceHistory>();

    public virtual Service Service { get; set; } = null!;

    public virtual AppointmentSlot Slot { get; set; } = null!;

    public virtual Vehicle Vehicle { get; set; } = null!;
}
