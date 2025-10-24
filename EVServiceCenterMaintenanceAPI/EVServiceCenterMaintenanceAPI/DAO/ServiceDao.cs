using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class ServiceDao
    {
        private readonly EvserviceCenterDbContext _context;

        public ServiceDao(EvserviceCenterDbContext context)
        {
            _context = context;
        }

        public async Task<Service?> GetActiveServiceByIdAsync(int serviceId)
        {
            return await _context.Services
                .FirstOrDefaultAsync(s => s.ServiceId == serviceId && s.Status == ServiceStatus.Active.ToString());
        }

        public async Task<(List<Service> Services, int Total)> GetAllActiveServicesAsync(ServiceQueryParams queryParams)
        {
            var validation = queryParams.Validate();
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage);
            }

            var query = _context.Services
                .Where(s => s.Status == ServiceStatus.Active.ToString())
                .AsQueryable();

            if (!string.IsNullOrEmpty(queryParams.Search))
                query = query.Where(s => s.ServiceName.Contains(queryParams.Search) || s.Description.Contains(queryParams.Search));
            if (queryParams.FromDate.HasValue)
                query = query.Where(s => s.CreatedAt >= queryParams.FromDate.Value);
            if (queryParams.ToDate.HasValue)
                query = query.Where(s => s.CreatedAt <= queryParams.ToDate.Value);

            if (!string.IsNullOrEmpty(queryParams.SortBy))
            {
                bool isAscending = queryParams.SortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);
                switch (queryParams.SortBy.ToLower())
                {
                    case "servicename":
                        query = isAscending ? query.OrderBy(s => s.ServiceName) : query.OrderByDescending(s => s.ServiceName);
                        break;
                    case "baseprice":
                        query = isAscending ? query.OrderBy(s => s.BasePrice) : query.OrderByDescending(s => s.BasePrice);
                        break;
                    case "createdat":
                        query = isAscending ? query.OrderBy(s => s.CreatedAt) : query.OrderByDescending(s => s.CreatedAt);
                        break;
                    default:
                        query = isAscending ? query.OrderBy(s => s.ServiceId) : query.OrderByDescending(s => s.ServiceId);
                        break;
                }
            }

            var total = await query.CountAsync();
            var services = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (services, total);
        }
    }
}
