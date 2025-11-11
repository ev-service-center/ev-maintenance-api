using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
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

        public async Task<(List<MaintenanceHistory> Histories, int Total)> GetAllMaintenanceHistoriesAsync(MaintenanceHistoryQueryParams queryParams)
        {
            var validation = queryParams.Validate();
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage);
            }

            var query = _context.MaintenanceHistories
                .Include(h => h.PartUsages)
                .AsQueryable();

            if (!string.IsNullOrEmpty(queryParams.Search))
                query = query.Where(h => h.Description.Contains(queryParams.Search) || h.Notes.Contains(queryParams.Search));
            if (queryParams.VehicleId.HasValue)
                query = query.Where(h => h.VehicleId == queryParams.VehicleId.Value);
            if (queryParams.FromDate.HasValue)
                query = query.Where(h => h.MaintenanceDate >= queryParams.FromDate.Value);
            if (queryParams.ToDate.HasValue)
                query = query.Where(h => h.MaintenanceDate <= queryParams.ToDate.Value);

            if (!string.IsNullOrEmpty(queryParams.SortBy))
            {
                bool isAscending = queryParams.SortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);
                switch (queryParams.SortBy.ToLower())
                {
                    case "maintenancedate":
                        query = isAscending ? query.OrderBy(h => h.MaintenanceDate) : query.OrderByDescending(h => h.MaintenanceDate);
                        break;
                    case "cost":
                        query = isAscending ? query.OrderBy(h => h.Cost) : query.OrderByDescending(h => h.Cost);
                        break;
                    default:
                        query = isAscending ? query.OrderBy(h => h.HistoryId) : query.OrderByDescending(h => h.HistoryId);
                        break;
                }
            }

            var total = await query.CountAsync();
            var histories = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (histories, total);
        }

        public async Task<List<MaintenanceHistory>> GetMaintenanceHistoriesByVehicleIdAsync(int vehicleId)
        {
            return await _context.MaintenanceHistories
                .Where(h => h.VehicleId == vehicleId)
                .Include(h => h.PartUsages)
                .ThenInclude(pu => pu.Part)
                .OrderByDescending(h => h.MaintenanceDate)
                .ToListAsync();
        }

        public async Task<MaintenanceHistory> UpdateMaintenanceHistoryAsync(MaintenanceHistory history)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingHistory = await _context.MaintenanceHistories.FirstOrDefaultAsync(h => h.HistoryId == history.HistoryId);
                if (existingHistory == null)
                    throw new Exception($"Maintenance history with ID {history.HistoryId} not found.");

                existingHistory.MaintenanceDate = history.MaintenanceDate;
                existingHistory.Description = history.Description;
                existingHistory.Notes = history.Notes;
                existingHistory.Cost = history.Cost;
                existingHistory.MileageAtMaintenance = history.MileageAtMaintenance;
                existingHistory.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return existingHistory;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to update maintenance history with ID {history.HistoryId}.", ex);
            }
        }

        public async Task<bool> DeleteMaintenanceHistoryAsync(int historyId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var history = await _context.MaintenanceHistories.FindAsync(historyId);
                if (history == null)
                    return false;

                _context.MaintenanceHistories.Remove(history);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to delete maintenance history with ID {historyId}.", ex);
            }
        }
    }
}
