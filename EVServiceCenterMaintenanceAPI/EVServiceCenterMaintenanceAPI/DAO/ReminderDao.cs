using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
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
