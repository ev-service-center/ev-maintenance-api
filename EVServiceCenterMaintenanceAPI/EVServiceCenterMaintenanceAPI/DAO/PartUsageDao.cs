using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class PartUsageDao
    {
        private readonly EvserviceCenterDbContext _context;

        public PartUsageDao(EvserviceCenterDbContext context)
        {
            _context = context;
        }

        public async Task<PartUsage> CreatePartUsageAsync(PartUsage partUsage)
        {
            // Validate MaintenanceHistory exists
            var historyExists = await _context.MaintenanceHistories
                .AnyAsync(h => h.HistoryId == partUsage.HistoryId);

            if (!historyExists)
            {
                throw new ArgumentException($"MaintenanceHistory with ID {partUsage.HistoryId} not found.");
            }

            // Get Part and validate
            var part = await _context.Parts
                .FirstOrDefaultAsync(p => p.PartId == partUsage.PartId);

            if (part == null)
            {
                throw new ArgumentException($"Part with ID {partUsage.PartId} not found.");
            }

            // Check if part is active
            if (part.Status != "Active")
            {
                throw new InvalidOperationException($"Part '{part.PartName}' is not active. Cannot use inactive parts.");
            }

            // Validate stock availability
            if (part.QuantityInStock == null || part.QuantityInStock < partUsage.QuantityUsed)
            {
                throw new InvalidOperationException(
                    $"Insufficient stock for part '{part.PartName}'. " +
                    $"Available: {part.QuantityInStock ?? 0}, Requested: {partUsage.QuantityUsed}");
            }

            if (partUsage.UnitCostPrice <= 0)
            {
                throw new ArgumentException($"UnitCostPrice must be greater than 0. Current value: {partUsage.UnitCostPrice}");
            }

            if (partUsage.UnitPrice <= 0)
            {
                throw new ArgumentException($"UnitPrice must be greater than 0. Current value: {partUsage.UnitPrice}");
            }

            if (partUsage.UnitCostPrice != part.CostPrice)
            {
                throw new InvalidOperationException(@$"UnitCostPrice mismatch. Expected: {part.CostPrice}, Provided: {partUsage.UnitCostPrice}. Prices must match the current part cost price in the database.");
            }

            if (partUsage.UnitPrice != part.Price)
            {
                throw new InvalidOperationException(@$"UnitPrice mismatch. Expected: {part.Price}, Provided: {partUsage.UnitPrice}. Prices must match the current part price in the database.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Check if a usage for the same part & history already exists
                var existingUsage = await _context.PartUsages
                    .FirstOrDefaultAsync(pu =>
                        pu.HistoryId == partUsage.HistoryId &&
                        pu.PartId == partUsage.PartId);

                PartUsage targetUsage;

                if (existingUsage != null)
                {
                    existingUsage.QuantityUsed += partUsage.QuantityUsed;
                    // Use current prices from DB (already verified above)
                    existingUsage.UnitCostPrice = part.CostPrice;
                    existingUsage.UnitPrice = part.Price;
                    targetUsage = existingUsage;
                    _context.PartUsages.Update(existingUsage);
                }
                else
                {
                    // Ensure prices are set from DB (already verified above)
                    partUsage.UnitCostPrice = part.CostPrice;
                    partUsage.UnitPrice = part.Price;
                    _context.PartUsages.Add(partUsage);
                    targetUsage = partUsage;
                }

                // 2. Deduct stock from Part
                part.QuantityInStock -= partUsage.QuantityUsed;
                part.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Load navigation properties for return
                await _context.Entry(targetUsage)
                    .Reference(pu => pu.Part)
                    .LoadAsync();
                await _context.Entry(targetUsage)
                    .Reference(pu => pu.History)
                    .LoadAsync();

                return targetUsage;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<PartUsage?> GetPartUsageByIdAsync(int usageId)
        {
            return await _context.PartUsages
                .Include(pu => pu.Part)
                .Include(pu => pu.History)
                    .ThenInclude(h => h.WorkOrder)
                .Include(pu => pu.History)
                    .ThenInclude(h => h.Vehicle)
                .FirstOrDefaultAsync(pu => pu.UsageId == usageId);
        }

        public async Task<List<PartUsage>> GetPartUsagesByHistoryIdAsync(int historyId)
        {
            return await _context.PartUsages
                .Include(pu => pu.Part)
                .Include(pu => pu.History)
                    .ThenInclude(h => h.WorkOrder)
                .Include(pu => pu.History)
                    .ThenInclude(h => h.Vehicle)
                .Where(pu => pu.HistoryId == historyId)
                .OrderBy(pu => pu.UsageId)
                .ToListAsync();
        }

        public async Task<List<PartUsage>> GetPartUsagesByPartIdAsync(int partId)
        {
            return await _context.PartUsages
                .Include(pu => pu.Part)
                .Include(pu => pu.History)
                    .ThenInclude(h => h.WorkOrder)
                .Include(pu => pu.History)
                    .ThenInclude(h => h.Vehicle)
                .Where(pu => pu.PartId == partId)
                .OrderByDescending(pu => pu.UsageId)
                .ToListAsync();
        }

        public async Task<List<PartUsage>> GetPartUsagesByWorkOrderIdAsync(int workOrderId)
        {
            return await _context.PartUsages
                .Include(pu => pu.Part)
                .Include(pu => pu.History)
                    .ThenInclude(h => h.WorkOrder)
                .Include(pu => pu.History)
                    .ThenInclude(h => h.Vehicle)
                .Where(pu => pu.History.WorkOrderId == workOrderId)
                .OrderBy(pu => pu.History.HistoryId)
                .ThenBy(pu => pu.UsageId)
                .ToListAsync();
        }

        public async Task<PartUsage> UpdatePartUsageAsync(PartUsage partUsage, int originalQuantity)
        {
            var part = partUsage.Part;
            if (part == null)
            {
                throw new InvalidOperationException($"Part for PartUsage {partUsage.UsageId} not found.");
            }

            // Start transaction
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // If quantity changed, adjust stock
                if (originalQuantity != partUsage.QuantityUsed)
                {
                    int quantityDifference = partUsage.QuantityUsed - originalQuantity;

                    // If increasing usage, check if stock available
                    if (quantityDifference > 0)
                    {
                        var currentStock = part.QuantityInStock ?? 0;
                        if (currentStock < quantityDifference)
                        {
                            throw new InvalidOperationException(
                                $"Insufficient stock to increase usage. " +
                                $"Available: {currentStock}, Additional needed: {quantityDifference}");
                        }
                    }

                    // Adjust stock (negative difference = return stock, positive = deduct more)
                    part.QuantityInStock = (part.QuantityInStock ?? 0) - quantityDifference;
                    part.UpdatedAt = DateTime.UtcNow;
                }

                // Update PartUsage entity (prices already set in Controller)
                _context.PartUsages.Update(partUsage);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Reload navigation properties
                await _context.Entry(partUsage)
                    .Reference(pu => pu.Part)
                    .LoadAsync();
                await _context.Entry(partUsage)
                    .Reference(pu => pu.History)
                    .LoadAsync();

                return partUsage;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Delete Part Usage and return stock to inventory
        /// </summary>
        public async Task<bool> DeletePartUsageAsync(int usageId)
        {
            var partUsage = await _context.PartUsages
                .Include(pu => pu.Part)
                .FirstOrDefaultAsync(pu => pu.UsageId == usageId);

            if (partUsage == null)
            {
                return false;
            }

            var part = partUsage.Part;

            // Start transaction
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Return stock to inventory
                if (part.QuantityInStock != null)
                {
                    part.QuantityInStock += partUsage.QuantityUsed;
                }
                else
                {
                    part.QuantityInStock = partUsage.QuantityUsed;
                }

                part.UpdatedAt = DateTime.UtcNow;

                // Delete PartUsage
                _context.PartUsages.Remove(partUsage);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<(bool IsAvailable, string Message, Part? Part)> CheckStockAvailabilityAsync(int partId, int requestedQuantity)
        {
            var part = await _context.Parts.FindAsync(partId);

            if (part == null)
            {
                return (false, $"Part with ID {partId} not found.", null);
            }

            if (part.Status != "Active")
            {
                return (false, $"Part '{part.PartName}' is not active.", part);
            }

            if (part.QuantityInStock == null || part.QuantityInStock < requestedQuantity)
            {
                return (false,
                    $"Insufficient stock for '{part.PartName}'. Available: {part.QuantityInStock ?? 0}, Requested: {requestedQuantity}",
                    part);
            }

            // Check if will be low stock after usage
            int remainingStock = (part.QuantityInStock ?? 0) - requestedQuantity;
            if (remainingStock < (part.MinStock ?? 0))
            {
                return (true,
                    $"Stock available but will be LOW after usage. Remaining: {remainingStock}, MinStock: {part.MinStock}",
                    part);
            }

            return (true, "Stock available.", part);
        }

        public async Task<(decimal TotalCost, decimal TotalPrice)> GetPartUsagesTotalAsync(int historyId)
        {
            var partUsages = await _context.PartUsages
                .Where(pu => pu.HistoryId == historyId)
                .ToListAsync();

            decimal totalCost = partUsages.Sum(pu => pu.UnitCostPrice * pu.QuantityUsed);
            decimal totalPrice = partUsages.Sum(pu => pu.UnitPrice * pu.QuantityUsed);

            return (totalCost, totalPrice);
        }

        public async Task<List<PartUsage>> GetPartUsagesByCustomerIdAsync(int customerId)
        {
            return await _context.PartUsages
                .Include(pu => pu.Part)
                .Include(pu => pu.History)
                    .ThenInclude(h => h.WorkOrder)
                .Include(pu => pu.History)
                    .ThenInclude(h => h.Vehicle)
                .Where(pu => pu.History != null && pu.History.WorkOrder != null && pu.History.WorkOrder.CustomerId == customerId)
                .OrderByDescending(pu => pu.History!.WorkOrderId)
                .ThenBy(pu => pu.History!.HistoryId)
                .ThenBy(pu => pu.UsageId)
                .ToListAsync();
        }

        public async Task<List<PartUsage>> GetPartUsagesByCenterIdAsync(int centerId)
        {
            return await _context.PartUsages
                .Include(pu => pu.Part)
                .Include(pu => pu.History)
                    .ThenInclude(h => h.WorkOrder)
                .Include(pu => pu.History)
                    .ThenInclude(h => h.Vehicle)
                .Where(pu => pu.Part.CenterId == centerId)
                .OrderByDescending(pu => pu.UsageId)
                .ToListAsync();
        }
    }
}

