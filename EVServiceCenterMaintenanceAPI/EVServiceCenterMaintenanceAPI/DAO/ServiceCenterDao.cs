using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
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

        public async Task<(List<ServiceCenter> Centers, int Total)> GetAllServiceCentersAsync(ServiceCenterQueryParams queryParams)
        {
            var validation = queryParams.Validate();
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage);
            }

            var query = _context.ServiceCenters.AsQueryable();

            if (!string.IsNullOrEmpty(queryParams.Search))
                query = query.Where(c => c.CenterName.Contains(queryParams.Search) || c.Address.Contains(queryParams.Search));

            // Filter by Status: Nếu có chọn Status thì dùng Status đó, nếu không thì ẩn Deleted
            if (queryParams.StatusServiceCenter.HasValue)
            {
                query = query.Where(c => c.Status == queryParams.StatusServiceCenter.ToString());
            }
            else
            {
                // Mặc định: không show Deleted
                query = query.Where(c => c.Status != "Deleted");
            }
            if (queryParams.FromDate.HasValue)
                query = query.Where(c => c.CreatedAt >= queryParams.FromDate.Value);
            if (queryParams.ToDate.HasValue)
                query = query.Where(c => c.CreatedAt <= queryParams.ToDate.Value);

            if (!string.IsNullOrEmpty(queryParams.SortBy))
            {
                bool isAscending = queryParams.SortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);
                switch (queryParams.SortBy.ToLower())
                {
                    case "centername":
                        query = isAscending ? query.OrderBy(c => c.CenterName) : query.OrderByDescending(c => c.CenterName);
                        break;
                    case "address":
                        query = isAscending ? query.OrderBy(c => c.Address) : query.OrderByDescending(c => c.Address);
                        break;
                    case "createdat":
                        query = isAscending ? query.OrderBy(c => c.CreatedAt) : query.OrderByDescending(c => c.CreatedAt);
                        break;
                    default:
                        query = isAscending ? query.OrderBy(c => c.CenterId) : query.OrderByDescending(c => c.CenterId);
                        break;
                }
            }

            var total = await query.CountAsync();
            var centers = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (centers, total);
        }

        public async Task DeleteServiceCenterAsync(int centerId)
        {
            var center = await _context.ServiceCenters.FindAsync(centerId);
            if (center == null)
                throw new KeyNotFoundException($"Service center with ID {centerId} not found.");

            // Soft delete: Set Status = Deleted
            center.Status = "Deleted";
            center.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
