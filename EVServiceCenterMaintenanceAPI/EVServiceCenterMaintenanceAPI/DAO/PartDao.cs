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
    }
}
