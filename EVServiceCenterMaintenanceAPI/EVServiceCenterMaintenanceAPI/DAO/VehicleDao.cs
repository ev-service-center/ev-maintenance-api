using EVServiceCenterMaintenanceAPI.Controllers;
using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.EntityFrameworkCore;
using EVServiceCenterMaintenanceAPI.Params;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class VehicleDao
    {
        private readonly EvserviceCenterDbContext _context;

        public VehicleDao(EvserviceCenterDbContext context)
        {
            _context = context;
        }

        public async Task<Vehicle> CreateVehicleAsync(Vehicle vehicle)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                vehicle.CreatedAt = DateTime.UtcNow;
                vehicle.UpdatedAt = DateTime.UtcNow;
                _context.Vehicles.Add(vehicle);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return vehicle;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("Failed to create vehicle.", ex);
            }
        }

        public async Task<Vehicle?> GetVehicleByIdAsync(int vehicleId)
        {
            return await _context.Vehicles
                .Include(v => v.MaintenanceHistories)
                .FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
        }

        public async Task<List<Vehicle>> GetVehiclesByCustomerIdAsync(int customerId)
        {
            return await _context.Vehicles
                .Where(v => v.CustomerId == customerId)
                .Include(v => v.MaintenanceHistories)
                .ToListAsync();
        }

        public async Task<(List<Vehicle> Vehicles, int Total)> GetAllVehiclesAsync(VehicleQueryParams queryParams)
        {
            var validation = queryParams.Validate();
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage);
            }

            var query = _context.Vehicles.Include(v => v.MaintenanceHistories).AsQueryable();
            if (!string.IsNullOrEmpty(queryParams.Search))
                query = query.Where(v => v.Model.Contains(queryParams.Search) || v.Vin.Contains(queryParams.Search) || v.Plate.Contains(queryParams.Search));
            if (queryParams.CustomerId.HasValue)
                query = query.Where(v => v.CustomerId == queryParams.CustomerId.Value);
            if (queryParams.FromDate.HasValue)
                query = query.Where(v => v.CreatedAt >= queryParams.FromDate.Value);
            if (queryParams.ToDate.HasValue)
                query = query.Where(v => v.CreatedAt <= queryParams.ToDate.Value);

            if (!string.IsNullOrEmpty(queryParams.SortBy))
            {
                bool isAscending = queryParams.SortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);
                switch (queryParams.SortBy.ToLower())
                {
                    case "model":
                        query = isAscending ? query.OrderBy(v => v.Model) : query.OrderByDescending(v => v.Model);
                        break;
                    case "vin":
                        query = isAscending ? query.OrderBy(v => v.Vin) : query.OrderByDescending(v => v.Vin);
                        break;
                    case "plate":
                        query = isAscending ? query.OrderBy(v => v.Plate) : query.OrderByDescending(v => v.Plate);
                        break;
                    case "createdat":
                        query = isAscending ? query.OrderBy(v => v.CreatedAt) : query.OrderByDescending(v => v.CreatedAt);
                        break;
                    default:
                        query = isAscending ? query.OrderBy(v => v.VehicleId) : query.OrderByDescending(v => v.VehicleId);
                        break;
                }
            }

            var total = await query.CountAsync();
            var vehicles = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (vehicles, total);
        }
    }
}
