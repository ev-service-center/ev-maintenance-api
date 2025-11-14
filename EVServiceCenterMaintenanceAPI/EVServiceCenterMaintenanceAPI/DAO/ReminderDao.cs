using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class ReminderDao
    {
        private readonly EvserviceCenterDbContext _context;

        public ReminderDao(EvserviceCenterDbContext context)
        {
            _context = context;
        }

        public async Task<Reminder?> GetReminderByIdAsync(int reminderId)
        {
            return await _context.Reminders.FirstOrDefaultAsync(r => r.ReminderId == reminderId);
        }

        public async Task<Reminder> CreateReminderAsync(Reminder reminder)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                reminder.CreatedAt = DateTime.UtcNow;
                reminder.UpdatedAt = DateTime.UtcNow;
                _context.Reminders.Add(reminder);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return reminder;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("Failed to create reminder.", ex);
            }
        }

        public async Task<(List<Reminder> Reminders, int Total)> GetAllRemindersAsync(ReminderQueryParams queryParams)
        {
            var validation = queryParams.Validate();
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage);
            }

            var query = _context.Reminders.AsQueryable();
            if (queryParams.ReminderType.HasValue)
                query = query.Where(r => r.ReminderType == queryParams.ReminderType.ToString());
            if (queryParams.Sent.HasValue)
                query = query.Where(r => r.Sent == queryParams.Sent.Value);
            if (queryParams.UserId.HasValue)
                query = query.Where(r => r.UserId == queryParams.UserId.Value);
            if (queryParams.FromDate.HasValue)
                query = query.Where(r => r.ReminderDate >= queryParams.FromDate.Value);
            if (queryParams.ToDate.HasValue)
                query = query.Where(r => r.ReminderDate <= queryParams.ToDate.Value);

            if (!string.IsNullOrEmpty(queryParams.SortBy))
            {
                bool isAscending = queryParams.SortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);
                switch (queryParams.SortBy.ToLower())
                {
                    case "reminderdate":
                        query = isAscending ? query.OrderBy(r => r.ReminderDate) : query.OrderByDescending(r => r.ReminderDate);
                        break;
                    default:
                        query = isAscending ? query.OrderBy(r => r.ReminderId) : query.OrderByDescending(r => r.ReminderId);
                        break;
                }
            }

            var total = await query.CountAsync();
            var reminders = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (reminders, total);
        }

        public async Task<List<Reminder>> GetRemindersByUserIdAsync(int userId)
        {
            return await _context.Reminders
                .Where(r => r.UserId == userId && r.Sent == false)
                .OrderBy(r => r.ReminderDate)
                .ToListAsync();
        }

        public async Task<Reminder> UpdateReminderAsync(Reminder reminder)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingReminder = await _context.Reminders.FirstOrDefaultAsync(r => r.ReminderId == reminder.ReminderId);
                if (existingReminder == null)
                    throw new Exception($"Reminder with ID {reminder.ReminderId} not found.");

                existingReminder.ReminderDate = reminder.ReminderDate;
                existingReminder.Message = reminder.Message;
                existingReminder.Sent = reminder.Sent;
                existingReminder.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return existingReminder;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to update reminder with ID {reminder.ReminderId}.", ex);
            }
        }

        public async Task GenerateRemindersForVehicleAsync(int vehicleId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var vehicle = await _context.Vehicles
                    .Include(v => v.MaintenanceHistories)
                    .Include(v => v.Customer)
                    .FirstOrDefaultAsync(v => v.VehicleId == vehicleId);

                if (vehicle == null)
                    throw new Exception($"Vehicle with ID {vehicleId} not found.");

                var lastMaintenance = vehicle.LastMaintenanceDate ?? vehicle.CreatedAt;
                var currentDate = DateTime.UtcNow;

                if ((currentDate - lastMaintenance).Days > 180)
                {
                    var reminder = new Reminder
                    {
                        UserId = vehicle.CustomerId,
                        VehicleId = vehicleId,
                        ReminderType = ReminderType.Maintenance.ToString(),
                        ReminderDate = currentDate.AddDays(7),
                        Message = "Your vehicle is due for maintenance based on time.",
                        Sent = false,
                        CreatedAt = currentDate,
                        UpdatedAt = currentDate
                    };
                    _context.Reminders.Add(reminder);
                }

                if (vehicle.CurrentMileage - (vehicle.MaintenanceHistories.LastOrDefault()?.MileageAtMaintenance ?? 0) > 10000)
                {
                    var reminder = new Reminder
                    {
                        UserId = vehicle.CustomerId,
                        VehicleId = vehicleId,
                        ReminderType = ReminderType.Maintenance.ToString(),
                        ReminderDate = currentDate.AddDays(7),
                        Message = "Your vehicle is due for maintenance based on mileage.",
                        Sent = false,
                        CreatedAt = currentDate,
                        UpdatedAt = currentDate
                    };
                    _context.Reminders.Add(reminder);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to generate reminders for vehicle with ID {vehicleId}.", ex);
            }
        }
    }
}
