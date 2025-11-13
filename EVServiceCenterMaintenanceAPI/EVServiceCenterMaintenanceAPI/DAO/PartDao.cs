using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class PartDao
    {
        private readonly EvserviceCenterDbContext _context;

        public PartDao(EvserviceCenterDbContext context)
        {
            _context = context;
        }

        public async Task<Part> CreatePartAsync(Part part)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                part.CreatedAt = DateTime.UtcNow;
                part.UpdatedAt = DateTime.UtcNow;
                _context.Parts.Add(part);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return part;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("Failed to create part.", ex);
            }
        }

        public async Task<Part?> GetPartByIdAsync(int partId)
        {
            return await _context.Parts.FirstOrDefaultAsync(p => p.PartId == partId);
        }

        public async Task<(List<Part> Parts, int Total)> GetAllPartsAsync(PartQueryParams queryParams)
        {
            var (IsValid, ErrorMessage) = queryParams.Validate();
            if (!IsValid)
            {
                throw new ArgumentException(ErrorMessage);
            }

            var query = queryParams.StatusPart.HasValue
                ? _context.Parts.IgnoreQueryFilters().AsQueryable()
                : _context.Parts.AsQueryable();

            if (!string.IsNullOrEmpty(queryParams.Search))
                query = query.Where(p => p.PartName.Contains(queryParams.Search));
            if (queryParams.CenterId.HasValue)
                query = query.Where(p => p.CenterId == queryParams.CenterId.Value);
            if (queryParams.StatusPart.HasValue)
                query = query.Where(p => p.Status == queryParams.StatusPart.ToString());
            if (queryParams.FromDate.HasValue)
                query = query.Where(p => p.CreatedAt >= queryParams.FromDate.Value);
            if (queryParams.ToDate.HasValue)
                query = query.Where(p => p.CreatedAt <= queryParams.ToDate.Value);

            if (!string.IsNullOrEmpty(queryParams.SortBy))
            {
                bool isAscending = queryParams.SortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);
                query = queryParams.SortBy.ToLower() switch
                {
                    "partname" => isAscending ? query.OrderBy(p => p.PartName) : query.OrderByDescending(p => p.PartName),
                    "price" => isAscending ? query.OrderBy(p => p.Price) : query.OrderByDescending(p => p.Price),
                    "quantityinstock" => isAscending ? query.OrderBy(p => p.QuantityInStock) : query.OrderByDescending(p => p.QuantityInStock),
                    "createdat" => isAscending ? query.OrderBy(p => p.CreatedAt) : query.OrderByDescending(p => p.CreatedAt),
                    _ => isAscending ? query.OrderBy(p => p.PartId) : query.OrderByDescending(p => p.PartId),
                };
            }
            else
            {
                query = query.OrderByDescending(p => p.PartId);
            }

            var total = await query.CountAsync();
            var parts = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (parts, total);
        }

        public async Task<List<PartSuggestionDto>> GetPartReorderSuggestionsAsync(int centerId)
        {
            var parts = await _context.Parts
                .Where(p => p.CenterId == centerId && p.QuantityInStock < p.MinStock)
                .ToListAsync();

            var suggestions = new List<PartSuggestionDto>();
            foreach (var part in parts)
            {
                var usage = await _context.PartUsages
                    .Where(pu => pu.PartId == part.PartId)
                    .GroupBy(pu => pu.History.MaintenanceDate.Month)
                    .Select(g => new { Month = g.Key, Count = g.Sum(pu => pu.QuantityUsed) })
                    .ToListAsync();

                var avgMonthlyUsage = usage.Any() ? usage.Average(u => u.Count) : 0;
                suggestions.Add(new PartSuggestionDto
                {
                    PartId = part.PartId,
                    PartName = part.PartName,
                    CurrentStock = part.QuantityInStock!.Value,
                    MinStock = part.MinStock!.Value,
                    SuggestedOrderQuantity = (int)Math.Ceiling(avgMonthlyUsage * 1.5 - part.QuantityInStock.Value)
                });
            }

            return suggestions;
        }

        public async Task<Part> UpdatePartAsync(Part part)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingPart = await _context.Parts.FirstOrDefaultAsync(p => p.PartId == part.PartId);
                if (existingPart == null)
                    throw new Exception($"Part with ID {part.PartId} not found.");

                existingPart.PartName = part.PartName;
                existingPart.Description = part.Description;
                existingPart.Price = part.Price;
                existingPart.QuantityInStock = part.QuantityInStock;
                existingPart.MinStock = part.MinStock;
                existingPart.Status = part.Status;
                existingPart.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return existingPart;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to update part with ID {part.PartId}.", ex);
            }
        }

        public async Task DeletePartAsync(int partId)
        {
            var part = await _context.Parts.FindAsync(partId);
            if (part == null)
                throw new KeyNotFoundException($"Part with ID {partId} not found.");

            // Soft delete: Set Status = Inactive
            part.Status = PartStatus.Inactive.ToString();
            part.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<(int Transferred, int Merged)> TransferPartsToAnotherCenterAsync(int sourceCenterId, int targetCenterId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Lấy TẤT CẢ parts của center nguồn
                var sourceParts = await _context.Parts
                    .Where(p => p.CenterId == sourceCenterId)
                    .ToListAsync();

                if (sourceParts.Count == 0)
                {
                    await transaction.CommitAsync();
                    return (0, 0); // Không có parts cần transfer
                }

                // Lấy TẤT CẢ parts của center đích
                var targetParts = await _context.Parts
                    .Where(p => p.CenterId == targetCenterId)
                    .ToListAsync();

                int transferredCount = 0;
                int mergedCount = 0;

                foreach (var sourcePart in sourceParts)
                {
                    // Tìm part tương tự ở center đích
                    var matchingTargetPart = targetParts.FirstOrDefault(tp =>
                        tp.PartName.Equals(sourcePart.PartName, StringComparison.OrdinalIgnoreCase) &&
                        tp.Price == sourcePart.Price);

                    if (matchingTargetPart != null)
                    {
                        // MERGE: Cộng dồn số lượng vào part đích
                        matchingTargetPart.QuantityInStock = (matchingTargetPart.QuantityInStock ?? 0) + (sourcePart.QuantityInStock ?? 0);
                        matchingTargetPart.UpdatedAt = DateTime.UtcNow;
                        mergedCount++;
                    }
                    else
                    {
                        // TRANSFER: Tạo Part MỚI ở center đích
                        var newPart = new Part
                        {
                            PartName = sourcePart.PartName,
                            Description = sourcePart.Description,
                            CostPrice = sourcePart.CostPrice,
                            Price = sourcePart.Price,
                            QuantityInStock = sourcePart.QuantityInStock,
                            MinStock = sourcePart.MinStock,
                            CenterId = targetCenterId,
                            Status = PartStatus.Active.ToString(),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.Parts.Add(newPart);
                        transferredCount++;
                    }

                    // Soft delete part nguồn và RESET quantity = 0
                    sourcePart.Status = PartStatus.Inactive.ToString();
                    sourcePart.QuantityInStock = 0;
                    sourcePart.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (transferredCount, mergedCount);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to transfer parts from center {sourceCenterId} to center {targetCenterId}.", ex);
            }
        }
    }
}
