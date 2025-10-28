using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class EmployeeDao
    {
        private readonly EvserviceCenterDbContext _context;
        public EmployeeDao(EvserviceCenterDbContext context)
        {
            _context = context;
        }

        public async Task<Employee> CreateEmployeeAsync(Employee employee)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                employee.CreatedAt = DateTime.UtcNow;
                employee.UpdatedAt = DateTime.UtcNow;
                _context.Employees.Add(employee);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return employee;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("Failed to create employee.", ex);
            }
        }

        public async Task<Employee?> GetEmployeeByIdAsync(int employeeId)
        {
            return await _context.Employees
                .Include(e => e.EmployeeNavigation)
                .Include(e => e.Center)
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        }
    }
}
