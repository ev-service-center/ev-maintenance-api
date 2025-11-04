using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class MaintenanceHistoryDao
    {
        private readonly EvserviceCenterDbContext _context;

        public MaintenanceHistoryDao(EvserviceCenterDbContext context)
        {
            _context = context;
        }

        public async Task<MaintenanceHistory> CreateMaintenanceHistoryAsync(MaintenanceHistory history)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                history.CreatedAt = DateTime.UtcNow;
                history.UpdatedAt = DateTime.UtcNow;
                _context.MaintenanceHistories.Add(history);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return history;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("Failed to create maintenance history.", ex);
            }
        }

        public async Task<MaintenanceHistory?> GetMaintenanceHistoryByIdAsync(int historyId)
        {
            return await _context.MaintenanceHistories
                .Include(h => h.PartUsages)
                .ThenInclude(pu => pu.Part)
                .FirstOrDefaultAsync(h => h.HistoryId == historyId);
        }
    }
}
