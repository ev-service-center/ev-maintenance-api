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

        public async Task<AppointmentService> AssignTechnicianAsync(AppointmentService appointmentService, int technicianId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.AppointmentServices.Attach(appointmentService);
                appointmentService.AssignedTechnicianId = technicianId;
                appointmentService.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return appointmentService;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to update AppointmentService with ID {appointmentService.AppointmentServiceId}.", ex);
            }
        }

        public async Task<AppointmentService> UpdateStatusAsync(AppointmentService appointmentService, string status)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.AppointmentServices.Attach(appointmentService);
                appointmentService.Status = status;
                appointmentService.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return appointmentService;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to update AppointmentService status with ID {appointmentService.AppointmentServiceId}.", ex);
            }
        }

        public async Task<AppointmentService> UpdateAppointmentServiceAsync(AppointmentService appointmentService)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.AppointmentServices.Attach(appointmentService);
                appointmentService.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return appointmentService;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to update AppointmentService with ID {appointmentService.AppointmentServiceId}.", ex);
            }
        }

        public async Task<List<AppointmentService>> GetAppointmentServicesByIdsAsync(List<int> ids)
        {
            return await _context.AppointmentServices
                .Include(aps => aps.WorkOrder)
                    .ThenInclude(wo => wo!.Center)
                .AsNoTracking()
                .Where(aps => ids.Contains(aps.AppointmentServiceId))
                .ToListAsync();
        }

        public async Task<List<AppointmentService>> BatchAssignTechnicianAsync(Dictionary<int, int> assignments)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var appointmentServiceIds = assignments.Keys.ToList();
                var appointmentServices = await _context.AppointmentServices
                    .Where(aps => appointmentServiceIds.Contains(aps.AppointmentServiceId))
                    .ToListAsync();

                foreach (var appointmentService in appointmentServices)
                {
                    if (assignments.TryGetValue(appointmentService.AppointmentServiceId, out int technicianId))
                    {
                        appointmentService.AssignedTechnicianId = technicianId;
                        appointmentService.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var results = await _context.AppointmentServices
                    .Include(aps => aps.Service)
                    .Include(aps => aps.WorkOrder)
                        .ThenInclude(wo => wo!.Center)
                    .Include(aps => aps.WorkOrder)
                        .ThenInclude(wo => wo!.Customer)
                    .Include(aps => aps.WorkOrder)
                        .ThenInclude(wo => wo!.Vehicle)
                    .AsNoTracking()
                    .Where(aps => appointmentServiceIds.Contains(aps.AppointmentServiceId))
                    .ToListAsync();

                return results;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("Failed to batch assign technicians.", ex);
            }
        }
    }
}