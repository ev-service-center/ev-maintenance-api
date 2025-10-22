using System;
using System.Collections.Generic;

namespace EVServiceCenterMaintenanceAPI.Models;

public partial class WorkOrder
{
    public int WorkOrderId { get; set; }

    public int CenterId { get; set; }

    public int CustomerId { get; set; }

    public int VehicleId { get; set; }

    public int CreatedByStaffId { get; set; }

    public int? AppointmentId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? CheckInAt { get; set; }

    public DateTime? CheckOutAt { get; set; }

    public int? OdometerKm { get; set; }

    public string? Notes { get; set; }

    public virtual Appointment? Appointment { get; set; }

    public virtual ICollection<AppointmentService> AppointmentServices { get; set; } = new List<AppointmentService>();

    public virtual ServiceCenter Center { get; set; } = null!;

    public virtual User Customer { get; set; } = null!;

    public virtual Invoice? Invoice { get; set; }

    public virtual ICollection<MaintenanceHistory> MaintenanceHistories { get; set; } = new List<MaintenanceHistory>();

    public virtual Vehicle Vehicle { get; set; } = null!;
}
