using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
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
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        }

        public async Task<(List<Employee> Employees, int Total)> GetAllEmployeesAsync(EmployeeQueryParams queryParams)
        {
            var (IsValid, ErrorMessage) = queryParams.Validate();
            if (!IsValid)
            {
                throw new ArgumentException(ErrorMessage);
            }

            var query = _context.Employees
                .Include(e => e.EmployeeNavigation)
                .Include(e => e.Center)
                .AsQueryable();

            if (!string.IsNullOrEmpty(queryParams.Search))
            {
                query = query.Where(e =>
                    e.EmployeeNavigation.FullName.Contains(queryParams.Search) ||
                    (e.Shift != null && e.Shift.Contains(queryParams.Search)));
            }

            if (queryParams.CenterId.HasValue)
                query = query.Where(e => e.CenterId == queryParams.CenterId.Value);

            if (queryParams.FromDate.HasValue)
                query = query.Where(e => e.CreatedAt >= queryParams.FromDate.Value);

            if (queryParams.ToDate.HasValue)
                query = query.Where(e => e.CreatedAt <= queryParams.ToDate.Value);

            if (!string.IsNullOrEmpty(queryParams.SortBy))
            {
                bool isAscending = queryParams.SortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);
                query = queryParams.SortBy.ToLower() switch
                {
                    "shift" => isAscending ? query.OrderBy(e => e.Shift) : query.OrderByDescending(e => e.Shift),
                    "performancescore" => isAscending ? query.OrderBy(e => e.PerformanceScore) : query.OrderByDescending(e => e.PerformanceScore),
                    "createdat" => isAscending ? query.OrderBy(e => e.CreatedAt) : query.OrderByDescending(e => e.CreatedAt),
                    _ => isAscending ? query.OrderBy(e => e.EmployeeId) : query.OrderByDescending(e => e.EmployeeId),
                };
            }
            else
            {
                query = query.OrderByDescending(e => e.EmployeeId);
            }

            var total = await query.CountAsync();
            var employees = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (employees, total);
        }

        public async Task<Employee> UpdateEmployeeAsync(Employee employee)
        {
            // Entity is already tracked and modified in controller
            // SaveChangesAsync() is already atomic - no transaction needed for single operation
            await _context.SaveChangesAsync();

            // Reload with includes to return full data
            return await _context.Employees
                .Include(e => e.EmployeeNavigation)
                .Include(e => e.Center)
                .FirstOrDefaultAsync(e => e.EmployeeId == employee.EmployeeId) ?? employee;
        }

        public async Task<bool> DeleteEmployeeAsync(int employeeId)
        {
            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null)
                return false;
            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
