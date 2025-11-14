using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class AppointmentServiceDao
    {
        private readonly EvserviceCenterDbContext _context;

        public AppointmentServiceDao(EvserviceCenterDbContext context)
        {
            _context = context;
        }

        public async Task<AppointmentService?> GetAppointmentServiceByIdAsync(int id)
        {
            return await _context.AppointmentServices
                .Include(aps => aps.Service)
                .Include(aps => aps.WorkOrder)
                    .ThenInclude(wo => wo!.Center)
                .Include(aps => aps.WorkOrder)
                    .ThenInclude(wo => wo!.Customer)
                .Include(aps => aps.WorkOrder)
                    .ThenInclude(wo => wo!.Vehicle)
                .AsNoTracking()
                .FirstOrDefaultAsync(aps => aps.AppointmentServiceId == id);
        }

        public async Task<AppointmentService> UpdateAppointmentServiceAsync(AppointmentService appointmentService)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingAppointmentService = await _context.AppointmentServices
                    .FirstOrDefaultAsync(aps => aps.AppointmentServiceId == appointmentService.AppointmentServiceId);

                if (existingAppointmentService == null)
                {
                    throw new Exception($"AppointmentService with ID {appointmentService.AppointmentServiceId} not found.");
                }

                existingAppointmentService.WorkOrderId = appointmentService.WorkOrderId;
                existingAppointmentService.ServiceId = appointmentService.ServiceId;
                existingAppointmentService.Price = appointmentService.Price;
                existingAppointmentService.AssignedTechnicianId = appointmentService.AssignedTechnicianId;
                existingAppointmentService.Status = appointmentService.Status;
                existingAppointmentService.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = await _context.AppointmentServices
                    .Include(aps => aps.Service)
                    .Include(aps => aps.WorkOrder)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(aps => aps.AppointmentServiceId == appointmentService.AppointmentServiceId);

                return result ?? throw new InvalidOperationException("Failed to retrieve updated AppointmentService.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to update AppointmentService with ID {appointmentService.AppointmentServiceId}.", ex);
            }
        }
    }
}