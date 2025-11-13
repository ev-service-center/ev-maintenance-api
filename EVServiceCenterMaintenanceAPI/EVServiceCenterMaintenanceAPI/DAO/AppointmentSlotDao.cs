using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Helpers;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class AppointmentSlotDao
    {
        private readonly EvserviceCenterDbContext _context;

        public AppointmentSlotDao(EvserviceCenterDbContext context)
        {
            _context = context;
        }

        public async Task<AppointmentSlot> CreateAppointmentSlotAsync(AppointmentSlot slot)
        {
            // Check for duplicate slot (same center, same time)
            var duplicate = await _context.AppointmentSlots
                .FirstOrDefaultAsync(s =>
                    s.CenterId == slot.CenterId &&
                    s.StartTime == slot.StartTime &&
                    s.EndTime == slot.EndTime);

            if (duplicate != null)
            {
                throw new ArgumentException(
                    $"Duplicate slot: A slot already exists for Center {slot.CenterId} " +
                    $"from {slot.StartTime:yyyy-MM-dd HH:mm} to {slot.EndTime:yyyy-MM-dd HH:mm}.");
            }

            slot.CreatedAt = DateTime.UtcNow;
            slot.UpdatedAt = DateTime.UtcNow;
            _context.AppointmentSlots.Add(slot);
            await _context.SaveChangesAsync();
            return slot;
        }

        public async Task<AppointmentSlot?> GetAppointmentSlotByIdAsync(int slotId)
        {
            return await _context.AppointmentSlots.FirstOrDefaultAsync(s => s.SlotId == slotId);
        }

        public async Task<AppointmentSlot?> GetAppointmentSlotWithAppointmentAsync(int slotId)
        {
            return await _context.AppointmentSlots
                .Include(s => s.Appointment)
                .FirstOrDefaultAsync(s => s.SlotId == slotId);
        }

        public async Task<AppointmentSlot> UpdateAppointmentSlotAsync(AppointmentSlot slot)
        {
            // Validate: Cannot set IsAvailable = true if there's an associated Appointment
            if (slot.IsAvailable == true && slot.Appointment != null)
            {
                throw new InvalidOperationException(
                    $"Cannot set IsAvailable to true for slot ID {slot.SlotId} because it has an associated appointment (ID: {slot.Appointment.AppointmentId}). " +
                    "Please cancel or reassign the appointment first.");
            }

            slot.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return slot;
        }

        public async Task<bool> DeleteAppointmentSlotAsync(int slotId)
        {
            var slot = await _context.AppointmentSlots
                .Include(s => s.Appointment)
                .FirstOrDefaultAsync(s => s.SlotId == slotId);

            if (slot == null)
                return false;

            //Cannot delete slot with an associated appointment
            if (slot.Appointment != null)
            {
                throw new InvalidOperationException(
                    $"Cannot delete slot ID {slotId} because it has an associated appointment (ID: {slot.Appointment.AppointmentId}). " +
                    "Please cancel or reassign the appointment first.");
            }

            // VALIDATION: Cannot delete booked slot (IsAvailable = false)
            if (!slot.IsAvailable)
            {
                throw new InvalidOperationException(
                    $"Cannot delete slot ID {slotId} because it is marked as unavailable (IsAvailable = false). " +
                    "This usually means it's booked. Please cancel the booking first.");
            }

            // Safe to delete - slot is empty and has no appointments
            _context.AppointmentSlots.Remove(slot);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<(List<AppointmentSlot> Slots, int Total)> GetAllAppointmentSlotsAsync(AppointmentSlotQueryParams queryParams)
        {
            var (IsValid, ErrorMessage) = queryParams.Validate();
            if (!IsValid)
            {
                throw new ArgumentException(ErrorMessage);
            }

            var query = _context.AppointmentSlots.AsQueryable();
            if (queryParams.CenterId.HasValue)
                query = query.Where(s => s.CenterId == queryParams.CenterId.Value);
            if (queryParams.IsAvailable.HasValue)
                query = query.Where(s => s.IsAvailable == queryParams.IsAvailable.Value);
            if (queryParams.FromDate.HasValue)
                query = query.Where(s => s.StartTime >= queryParams.FromDate.Value);
            if (queryParams.ToDate.HasValue)
                query = query.Where(s => s.StartTime <= queryParams.ToDate.Value);

            if (!string.IsNullOrEmpty(queryParams.SortBy))
            {
                bool isAscending = queryParams.SortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);
                query = queryParams.SortBy.ToLower() switch
                {
                    "starttime" => isAscending ? query.OrderBy(s => s.StartTime) : query.OrderByDescending(s => s.StartTime),
                    "endtime" => isAscending ? query.OrderBy(s => s.EndTime) : query.OrderByDescending(s => s.EndTime),
                    _ => isAscending ? query.OrderBy(s => s.SlotId) : query.OrderByDescending(s => s.SlotId),
                };
            }

            var total = await query.CountAsync();
            var slots = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (slots, total);
        }
        public async Task<List<AppointmentSlot>> GetAvailableSlotsAsync(int centerId, DateTime startDate, DateTime endDate)
        {
            var todayVietnam = TimeZoneHelper.TodayInVietnam;

            var start = startDate.Date;
            var end = endDate.Date;

            // Validate date range
            if (start < todayVietnam || end < todayVietnam)
                throw new ArgumentException("Cannot process past dates.");

            if (start > end)
                throw new ArgumentException("Start date must be earlier than or equal to end date.");

            var slots = await _context.AppointmentSlots
                .Where(s => s.CenterId == centerId &&
                            s.StartTime.Date >= startDate.Date &&
                            s.StartTime.Date <= endDate.Date)
                .OrderBy(s => s.StartTime)
                .ToListAsync();

            // Filter out past or too-soon slots (< NOW + 1 hour)
            var nowVietnam = TimeZoneHelper.NowInVietnam;
            var minAllowedTime = nowVietnam.AddHours(1);

            return slots
                .Where(s => s.StartTime.Date != nowVietnam.Date || s.StartTime >= minAllowedTime)
                .Where(s => s.IsAvailable)
                .ToList();
        }
    }
}

