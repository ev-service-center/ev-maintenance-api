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

        public async Task<(List<Service> Services, int Total)> GetAllServicesAsync(ServiceQueryParams queryParams)
        {
            var validation = queryParams.Validate();
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage);
            }

            var query = _context.Services.AsNoTracking().AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(queryParams.Search))
                query = query.Where(s => s.ServiceName.Contains(queryParams.Search) || (s.Description != null && s.Description.Contains(queryParams.Search)));

            if (queryParams.StatusService.HasValue)
                query = query.Where(s => s.Status == queryParams.StatusService.ToString());

            if (queryParams.FromDate.HasValue)
                query = query.Where(s => s.CreatedAt >= queryParams.FromDate.Value);

            if (queryParams.ToDate.HasValue)
                query = query.Where(s => s.CreatedAt <= queryParams.ToDate.Value);

            // Apply sorting
            query = ApplySorting(query, queryParams.SortBy, queryParams.SortOrder);

            var total = await query.CountAsync();
            var services = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (services, total);
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
                .AsNoTracking()
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(queryParams.Search))
                query = query.Where(s => s.ServiceName.Contains(queryParams.Search) || (s.Description != null && s.Description.Contains(queryParams.Search)));

            if (queryParams.FromDate.HasValue)
                query = query.Where(s => s.CreatedAt >= queryParams.FromDate.Value);

            if (queryParams.ToDate.HasValue)
                query = query.Where(s => s.CreatedAt <= queryParams.ToDate.Value);

            // Apply sorting
            query = ApplySorting(query, queryParams.SortBy, queryParams.SortOrder);

            var total = await query.CountAsync();
            var services = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (services, total);
        }

        public async Task<Service> UpdateServiceAsync(Service service)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingService = await _context.Services.FirstOrDefaultAsync(s => s.ServiceId == service.ServiceId);
                if (existingService == null)
                    throw new Exception($"Service with ID {service.ServiceId} not found.");

                existingService.ServiceName = service.ServiceName;
                existingService.Description = service.Description;
                existingService.BasePrice = service.BasePrice;
                existingService.EstimatedTime = service.EstimatedTime;
                existingService.Status = service.Status;
                existingService.ReminderIntervalDays = service.ReminderIntervalDays;
                existingService.ReminderMileage = service.ReminderMileage;
                existingService.Notes = service.Notes;
                existingService.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return existingService;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to update service with ID {service.ServiceId}.", ex);
            }
        }

        public async Task<bool> DeleteServiceAsync(int serviceId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var service = await _context.Services.FindAsync(serviceId);
                if (service == null)
                    return false;

                _context.Services.Remove(service);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to delete service with ID {serviceId}.", ex);
            }
        }

        private static IQueryable<Service> ApplySorting(IQueryable<Service> query, string? sortBy, string sortOrder)
        {
            if (string.IsNullOrEmpty(sortBy))
                return query.OrderBy(s => s.ServiceId);

            bool isAscending = sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);

            return sortBy.ToLower() switch
            {
                "servicename" => isAscending ? query.OrderBy(s => s.ServiceName) : query.OrderByDescending(s => s.ServiceName),
                "baseprice" => isAscending ? query.OrderBy(s => s.BasePrice) : query.OrderByDescending(s => s.BasePrice),
                "createdat" => isAscending ? query.OrderBy(s => s.CreatedAt) : query.OrderByDescending(s => s.CreatedAt),
                _ => isAscending ? query.OrderBy(s => s.ServiceId) : query.OrderByDescending(s => s.ServiceId)
            };
        }
    }
}
