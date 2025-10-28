using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class ServiceCenterDao
    {
        private readonly EvserviceCenterDbContext _context;

        public ServiceCenterDao(EvserviceCenterDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task<bool> IsExistServiceCenterAsync(int serviceCenterId)
        {
            return _context.ServiceCenters.AnyAsync(s => s.CenterId == serviceCenterId);
        }

        public async Task<ServiceCenter> CreateServiceCenterAsync(ServiceCenter center)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                center.CreatedAt = DateTime.UtcNow;
                center.UpdatedAt = DateTime.UtcNow;
                _context.ServiceCenters.Add(center);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return center;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to create service center: {ex.Message}", ex);
            }
        }

        public async Task<ServiceCenter?> GetServiceCenterByIdAsync(int centerId)
        {
            return await _context.ServiceCenters
                .Include(c => c.Appointments)
                .Include(c => c.Parts)
                .FirstOrDefaultAsync(c => c.CenterId == centerId);
        }
    }
}
