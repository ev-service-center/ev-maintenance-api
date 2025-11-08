using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.EntityFrameworkCore;
using EVServiceCenterMaintenanceAPI.Params;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Utils;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class AppointmentDao
    {
        private readonly EvserviceCenterDbContext _context;
        private readonly IServiceScopeFactory _scopeFactory;

        public AppointmentDao(EvserviceCenterDbContext context, IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _scopeFactory = scopeFactory;
        }

        public async Task<Appointment> CreateAppointmentAsync(Appointment appointment)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                appointment.CreatedAt = DateTime.UtcNow;
                appointment.UpdatedAt = DateTime.UtcNow;
                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return appointment;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("Failed to create appointment." + ex.Message, ex);
            }
        }

        public async Task<Appointment?> GetAppointmentByIdAsync(int appointmentId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<EvserviceCenterDbContext>();
            return await context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Vehicle)
                .Include(a => a.Center)
                .Include(a => a.WorkOrders)
                    .ThenInclude(w => w.AppointmentServices)
                        .ThenInclude(aps => aps.Service)
                .Include(a => a.Slot)
                .Include(a => a.AssignedTechnician)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);
        }

        public async Task<Appointment> UpdateAppointmentStatusAsync(int appointmentId, AppointmentStatus statusApp)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingAppointment = await _context.Appointments
                    .Include(a => a.WorkOrders)
                        .ThenInclude(w => w.AppointmentServices)
                            .ThenInclude(aps => aps.Service)
                    .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);
                if (existingAppointment == null)
                    throw new Exception($"Appointment with ID {appointmentId} not found.");

                existingAppointment.Status = statusApp.ToString();
                existingAppointment.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (existingAppointment.Status == AppointmentStatus.Completed.ToString())
                {
                    TaskHelper.FireAndForget(existingAppointment, AddToReminderNotificationAsync);
                }

                return existingAppointment;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to update status appointment with ID {appointmentId}.", ex);
            }
        }

        public async Task<Appointment> UpdateAppointmentAsync(Appointment appointment)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingAppointment = await _context.Appointments
                    .Include(a => a.WorkOrders)
                        .ThenInclude(w => w.AppointmentServices)
                            .ThenInclude(aps => aps.Service)
                    .FirstOrDefaultAsync(a => a.AppointmentId == appointment.AppointmentId);
                if (existingAppointment == null)
                    throw new Exception($"Appointment with ID {appointment.AppointmentId} not found.");

                existingAppointment.AppointmentDate = appointment.AppointmentDate;
                existingAppointment.Status = appointment.Status;
                existingAppointment.Notes = appointment.Notes;
                existingAppointment.AssignedTechnicianId = appointment.AssignedTechnicianId;
                existingAppointment.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (existingAppointment.Status == AppointmentStatus.Completed.ToString())
                {
                    TaskHelper.FireAndForget(existingAppointment, AddToReminderNotificationAsync);
                }

                return existingAppointment;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to update appointment with ID {appointment.AppointmentId}.", ex);
            }
        }

        public async Task<bool> DeleteAppointmentAsync(int appointmentId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var appointment = await _context.Appointments.FindAsync(appointmentId);
                if (appointment == null)
                    return false;

                _context.Appointments.Remove(appointment);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to delete appointment with ID {appointmentId}.", ex);
            }
        }

        public async Task<(List<Appointment> Appointments, int Total)> GetAllAppointmentsAsync(AppointmentQueryParams queryParams)
        {
            var validation = queryParams.Validate();
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage);
            }

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<EvserviceCenterDbContext>();

            var query = context.Appointments
                .Include(a => a.Slot)
                .Include(a => a.Customer)
                .Include(a => a.Vehicle)
                .Include(a => a.WorkOrders)
                    .ThenInclude(w => w.AppointmentServices)
                        .ThenInclude(aps => aps.Service)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrEmpty(queryParams.Search))
                query = query.Where(a => a.Notes != null && a.Notes.Contains(queryParams.Search));
            if (queryParams.StatusAppointment != null)
                query = query.Where(a => a.Status == queryParams.StatusAppointment.ToString());
            if (queryParams.CenterId.HasValue)
                query = query.Where(a => a.CenterId == queryParams.CenterId.Value);
            if (queryParams.CustomerId.HasValue)
                query = query.Where(a => a.CustomerId == queryParams.CustomerId.Value);
            if (queryParams.TechnicianId.HasValue)
                query = query.Where(a => a.AssignedTechnicianId == queryParams.TechnicianId.Value);
            if (queryParams.FromDate.HasValue)
                query = query.Where(a => a.AppointmentDate >= queryParams.FromDate.Value);
            if (queryParams.ToDate.HasValue)
                query = query.Where(a => a.AppointmentDate <= queryParams.ToDate.Value);

            if (!string.IsNullOrEmpty(queryParams.SortBy))
            {
                bool isAscending = queryParams.SortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);
                switch (queryParams.SortBy.ToLower())
                {
                    case "appointmentdate":
                        query = isAscending ? query.OrderBy(a => a.AppointmentDate) : query.OrderByDescending(a => a.AppointmentDate);
                        break;
                    case "status":
                        query = isAscending ? query.OrderBy(a => a.Status) : query.OrderByDescending(a => a.Status);
                        break;
                    default:
                        query = isAscending ? query.OrderBy(a => a.AppointmentId) : query.OrderByDescending(a => a.AppointmentId);
                        break;
                }
            }

            var total = await query.CountAsync();
            var appointments = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (appointments, total);
        }

        public async Task<List<Appointment>> GetAppointmentsByCustomerIdAsync(int customerId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<EvserviceCenterDbContext>();
            return await context.Appointments
                .Where(a => a.CustomerId == customerId)
                .Include(a => a.WorkOrders)
                    .ThenInclude(w => w.AppointmentServices)
                        .ThenInclude(aps => aps.Service)
                .Include(a => a.Center)
                .AsNoTracking()
                .OrderByDescending(a => a.AppointmentDate)
                .ToListAsync();
        }

        public async Task<List<Appointment>> GetAppointmentsByTechnicianIdAsync(int technicianId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<EvserviceCenterDbContext>();
            return await context.Appointments
                .Where(a => a.AssignedTechnicianId == technicianId)
                .Include(a => a.Vehicle)
                .Include(a => a.WorkOrders)
                    .ThenInclude(w => w.AppointmentServices)
                        .ThenInclude(aps => aps.Service)
                .AsNoTracking()
                .OrderBy(a => a.AppointmentDate)
                .ToListAsync();
        }

        public async Task<Appointment> AssignTechnicianAsync(int appointmentId, int technicianId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var appointment = await _context.Appointments
                    .Include(a => a.WorkOrders)
                        .ThenInclude(w => w.AppointmentServices)
                            .ThenInclude(aps => aps.Service)
                    .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);
                if (appointment == null)
                    throw new Exception($"Appointment with ID {appointmentId} not found.");

                appointment.AssignedTechnicianId = technicianId;
                appointment.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return appointment;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to assign technician to appointment with ID {appointmentId}.", ex);
            }
        }

        private async Task AddToReminderNotificationAsync(Appointment app)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app), "Appointment cannot be null.");

            if (app.Status != AppointmentStatus.Completed.ToString())
                return;

            // Get the first WorkOrder's first AppointmentService for the reminder
            var workOrder = app.WorkOrders?.FirstOrDefault();
            var appointmentService = workOrder?.AppointmentServices?.FirstOrDefault();
            var serviceName = appointmentService?.Service?.ServiceName ?? "của chúng tôi";
            var serviceId = appointmentService?.ServiceId ?? 0;

            var reminder = new Reminder
            {
                UserId = app.CustomerId,
                ServiceId = serviceId,
                VehicleId = app.VehicleId,
                ReminderType = ReminderType.Rating.ToString(),
                ReminderDate = app.AppointmentDate.AddDays(7),
                Message = $"Cảm ơn bạn đã sử dụng dịch vụ {serviceName}. Vui lòng đánh giá trải nghiệm của bạn!",
                Sent = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Reminders.Add(reminder);
            await _context.SaveChangesAsync();
        }
    }
}