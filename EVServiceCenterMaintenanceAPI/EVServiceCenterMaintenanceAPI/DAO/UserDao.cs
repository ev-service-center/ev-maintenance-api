using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class UserDao
    {
        private readonly EvserviceCenterDbContext _context;

        public UserDao(EvserviceCenterDbContext context)
        {
            _context = context;

        }

        public async Task<User> CreateUserAsync(User user, string password)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            user.CreatedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User> RegisterUserAsync(User user, string password, AuthToken token)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                user.CreatedAt = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                token.UserId = user.UserId;
                _context.AuthTokens.Add(token);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return user;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("Failed to create user.", ex);
            }
        }

        public async Task<bool> IsUsernameExistsAsync(string username)
        {
            return await _context.Users.AnyAsync(u => u.Username == username);
        }

        public async Task<bool> IsEmailExistsAsync(string email)
        {
            return await _context.Users.AnyAsync(u => u.Email == email);
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _context.Users
                .Include(u => u.Vehicles)
                .FirstOrDefaultAsync(u => u.UserId == userId);
        }

        public async Task<User?> GetUserByEmailOrUsernameAsync(string emailOrUsername)
        {
            return await _context.Users
                .Include(u => u.Vehicles)
                .FirstOrDefaultAsync(u => u.Email == emailOrUsername || u.Username == emailOrUsername);
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.Users
                .Include(u => u.Vehicles)
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User> UpdateUserAsync(User user, string? newPassword = null)
        {
            if (!string.IsNullOrEmpty(newPassword))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User> UpdatePasswordAsync(int userId, string oldPassword, string newPassword)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                throw new Exception($"User with ID {userId} not found.");

            if (!BCrypt.Net.BCrypt.Verify(oldPassword, user.PasswordHash))
                throw new UnauthorizedAccessException("Invalid old password.");

            if (oldPassword == newPassword)
                throw new ArgumentException("New password must be different from old password.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<(List<User> Users, int Total)> GetAllUsersAsync(UserQueryParams queryParams, HashSet<int>? centerUserIds = null)
        {
            var (IsValid, ErrorMessage) = queryParams.Validate();
            if (!IsValid)
            {
                throw new ArgumentException(ErrorMessage);
            }

            var query = queryParams.StatusUser.HasValue
                ? _context.Users.IgnoreQueryFilters().AsQueryable()
                : _context.Users.AsQueryable();

            if (centerUserIds != null)
            {
                query = query.Where(u => u.Role != "Admin");
            }

            if (!string.IsNullOrEmpty(queryParams.Search))
                query = query.Where(u => u.Username.Contains(queryParams.Search) || u.FullName.Contains(queryParams.Search) || u.Email.Contains(queryParams.Search));
            if (queryParams.Role.HasValue)
                query = query.Where(u => u.Role == queryParams.Role.ToString());
            if (queryParams.StatusUser.HasValue)
                query = query.Where(u => u.Status == queryParams.StatusUser.ToString());
            if (queryParams.FromDate.HasValue)
                query = query.Where(u => u.CreatedAt >= queryParams.FromDate.Value);
            if (queryParams.ToDate.HasValue)
                query = query.Where(u => u.CreatedAt <= queryParams.ToDate.Value);

            if (queryParams.WithoutEmployee == true)
            {
                query = query.Where(u => !_context.Employees.Any(e => e.EmployeeId == u.UserId));
            }

            if (centerUserIds != null)
            {
                query = query.Where(u =>
                    u.Role != UserRole.Staff.ToString() && u.Role != UserRole.Technician.ToString() || centerUserIds.Contains(u.UserId) // Customer và các role khác (không filter theo center)
                );
            }

            query = ApplySorting(query, queryParams.SortBy, queryParams.SortOrder);

            var total = await query.CountAsync();
            var users = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (users, total);
        }

        public async Task DeleteUserAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                throw new KeyNotFoundException($"User with ID {userId} not found.");

            // Soft delete: Set Status = Deleted
            user.Status = UserStatus.Deleted.ToString();
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<List<User>> GetUsersByRoleAsync(UserRole role)
        {
            return await _context.Users.Where(u => u.Role == role.ToString()).ToListAsync();
        }

        public async Task<(List<User> Users, int Total)> GetUsersByRoleAsync(UserRole role, UserRoleQueryParams queryParams, HashSet<int>? centerUserIds = null)
        {
            var (IsValid, ErrorMessage) = queryParams.Validate();
            if (!IsValid)
            {
                throw new ArgumentException(ErrorMessage);
            }

            var query = _context.Users.Where(u => u.Role == role.ToString());

            if (centerUserIds != null && role == UserRole.Admin)
            {
                query = query.Where(u => false); // Always false, không có kết quả
            }

            if (!string.IsNullOrEmpty(queryParams.Search))
                query = query.Where(u => u.Username.Contains(queryParams.Search) || u.FullName.Contains(queryParams.Search) || u.Email.Contains(queryParams.Search));

            if (queryParams.StatusUser.HasValue)
                query = query.Where(u => u.Status == queryParams.StatusUser.ToString());

            if (queryParams.FromDate.HasValue)
                query = query.Where(u => u.CreatedAt >= queryParams.FromDate.Value);

            if (queryParams.ToDate.HasValue)
                query = query.Where(u => u.CreatedAt <= queryParams.ToDate.Value);

            if (centerUserIds != null && (role == UserRole.Staff || role == UserRole.Technician))
            {
                query = query.Where(u => centerUserIds.Contains(u.UserId));
            }

            query = ApplySorting(query, queryParams.SortBy, queryParams.SortOrder);

            var total = await query.CountAsync();
            var users = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (users, total);
        }

        private static IQueryable<User> ApplySorting(IQueryable<User> query, string? sortBy, string sortOrder)
        {
            if (sortBy == null || string.IsNullOrWhiteSpace(sortBy))
                return query.OrderByDescending(u => u.UserId);

            bool isAscending = sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);

            return sortBy.ToLower() switch
            {
                "username" => isAscending ? query.OrderBy(u => u.Username) : query.OrderByDescending(u => u.Username),
                "fullname" => isAscending ? query.OrderBy(u => u.FullName) : query.OrderByDescending(u => u.FullName),
                "email" => isAscending ? query.OrderBy(u => u.Email) : query.OrderByDescending(u => u.Email),
                "createdat" => isAscending ? query.OrderBy(u => u.CreatedAt) : query.OrderByDescending(u => u.CreatedAt),
                _ => isAscending ? query.OrderBy(u => u.UserId) : query.OrderByDescending(u => u.UserId)
            };
        }
    }
}
