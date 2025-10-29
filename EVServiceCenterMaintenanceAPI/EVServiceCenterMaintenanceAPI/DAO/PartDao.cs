using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Models;
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
    }
}
