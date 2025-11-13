using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class ServiceCenterDao
    {
        private readonly EvserviceCenterDbContext _context;
        private readonly PartDao _partDao;

        public ServiceCenterDao(EvserviceCenterDbContext context, PartDao partDao)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _partDao = partDao ?? throw new ArgumentNullException(nameof(partDao));
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
            var (IsValid, ErrorMessage) = queryParams.Validate();
            if (!IsValid)
            {
                throw new ArgumentException(ErrorMessage);
            }

            var query = queryParams.StatusServiceCenter.HasValue
                ? _context.ServiceCenters.IgnoreQueryFilters().AsQueryable()
                : _context.ServiceCenters.AsQueryable();

            if (!string.IsNullOrEmpty(queryParams.Search))
                query = query.Where(c => c.CenterName.Contains(queryParams.Search) || c.Address.Contains(queryParams.Search));
            if (queryParams.StatusServiceCenter.HasValue)
                query = query.Where(c => c.Status == queryParams.StatusServiceCenter.ToString());
            if (queryParams.FromDate.HasValue)
                query = query.Where(c => c.CreatedAt >= queryParams.FromDate.Value);
            if (queryParams.ToDate.HasValue)
                query = query.Where(c => c.CreatedAt <= queryParams.ToDate.Value);

            if (!string.IsNullOrEmpty(queryParams.SortBy))
            {
                bool isAscending = queryParams.SortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);
                query = queryParams.SortBy.ToLower() switch
                {
                    "centername" => isAscending ? query.OrderBy(c => c.CenterName) : query.OrderByDescending(c => c.CenterName),
                    "address" => isAscending ? query.OrderBy(c => c.Address) : query.OrderByDescending(c => c.Address),
                    "createdat" => isAscending ? query.OrderBy(c => c.CreatedAt) : query.OrderByDescending(c => c.CreatedAt),
                    _ => isAscending ? query.OrderBy(c => c.CenterId) : query.OrderByDescending(c => c.CenterId),
                };
            }
            else
            {
                query = query.OrderByDescending(c => c.CenterId);
            }

            var total = await query.CountAsync();
            var centers = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (centers, total);
        }

        public async Task<(int Transferred, int Merged)> DeleteServiceCenterAsync(int centerId, int targetCenterId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate center nguồn tồn tại
                var center = await _context.ServiceCenters.FindAsync(centerId);
                if (center == null)
                    throw new KeyNotFoundException($"Service center with ID {centerId} not found.");

                // Validate center đích tồn tại
                var targetCenter = await _context.ServiceCenters.FindAsync(targetCenterId);
                if (targetCenter == null)
                    throw new KeyNotFoundException($"Target service center with ID {targetCenterId} not found.");

                // Validate center đích không phải là center nguồn
                if (centerId == targetCenterId)
                    throw new ArgumentException("Target center must be different from the center being deleted.");

                // Validate center đích không bị Deleted
                if (targetCenter.Status == ServiceCenterStatus.Deleted.ToString())
                    throw new ArgumentException($"Cannot transfer parts to a deleted service center (ID: {targetCenterId}).");

                // Transfer tất cả parts sang center đích
                var (transferred, merged) = await _partDao.TransferPartsToAnotherCenterAsync(centerId, targetCenterId);

                // Soft delete center nguồn
                center.Status = ServiceCenterStatus.Deleted.ToString();
                center.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return (transferred, merged);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ServiceCenter> UpdateServiceCenterAsync(ServiceCenter center)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingCenter = await _context.ServiceCenters.FirstOrDefaultAsync(c => c.CenterId == center.CenterId);
                if (existingCenter == null)
                    throw new Exception($"Service center with ID {center.CenterId} not found.");

                existingCenter.CenterName = center.CenterName;
                existingCenter.Address = center.Address;
                existingCenter.Phone = center.Phone;
                existingCenter.Email = center.Email;
                existingCenter.Status = center.Status;
                existingCenter.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return existingCenter;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to update service center with ID {center.CenterId}: {ex.Message}", ex);
            }
        }

        public async Task<ServiceCenter?> GetActiveServiceCenterByIdAsync(int centerId)
        {
            return await _context.ServiceCenters
                .Include(c => c.Appointments)
                .Include(c => c.Parts)
                .FirstOrDefaultAsync(c => c.CenterId == centerId && c.Status == ServiceCenterStatus.Open.ToString());
        }

        public async Task<(List<ServiceCenter> Centers, int Total)> GetAllActiveServiceCentersAsync(ServiceCenterQueryParams queryParams)
        {
            var validation = queryParams.Validate();
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage);
            }

            var query = _context.ServiceCenters
                .Where(c => c.Status == ServiceCenterStatus.Open.ToString())
                .AsQueryable();

            if (!string.IsNullOrEmpty(queryParams.Search))
                query = query.Where(c => c.CenterName.Contains(queryParams.Search) || c.Address.Contains(queryParams.Search));
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
            else
            {
                query = query.OrderByDescending(c => c.CenterId);
            }

            var total = await query.CountAsync();
            var centers = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (centers, total);
        }
    }
}
