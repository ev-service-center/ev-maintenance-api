using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.EntityFrameworkCore;

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
    }
}
