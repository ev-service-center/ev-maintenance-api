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

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.Users
                .Include(u => u.Vehicles)
                .FirstOrDefaultAsync(u => u.Email == email || u.Username == email);
        }

        public async Task<User> UpdateUserAsync(User user, string? newPassword = null)
        {
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == user.UserId);
            if (existingUser == null)
                throw new Exception($"User with ID {user.UserId} not found.");

            existingUser.FullName = user.FullName;
            existingUser.Email = user.Email;
            existingUser.Phone = user.Phone;
            existingUser.Status = user.Status;
            existingUser.Avatar = user.Avatar;
            existingUser.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(newPassword))
                existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

            await _context.SaveChangesAsync();
            return existingUser;
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

        public async Task<(List<User> Users, int Total)> GetAllUsersAsync(UserQueryParams queryParams)
        {
            var validation = queryParams.Validate();
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage);
            }

            var query = _context.Users.AsQueryable();
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

            if (!string.IsNullOrEmpty(queryParams.SortBy))
            {
                bool isAscending = queryParams.SortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);
                switch (queryParams.SortBy.ToLower())
                {
                    case "username":
                        query = isAscending ? query.OrderBy(u => u.Username) : query.OrderByDescending(u => u.Username);
                        break;
                    case "fullname":
                        query = isAscending ? query.OrderBy(u => u.FullName) : query.OrderByDescending(u => u.FullName);
                        break;
                    case "email":
                        query = isAscending ? query.OrderBy(u => u.Email) : query.OrderByDescending(u => u.Email);
                        break;
                    case "createdat":
                        query = isAscending ? query.OrderBy(u => u.CreatedAt) : query.OrderByDescending(u => u.CreatedAt);
                        break;
                    default:
                        query = isAscending ? query.OrderBy(u => u.UserId) : query.OrderByDescending(u => u.UserId);
                        break;
                }
            }

            var total = await query.CountAsync();
            var users = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (users, total);
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<User>> GetUsersByRoleAsync(UserRole role)
        {
            return await _context.Users.Where(u => u.Role == role.ToString()).ToListAsync();
        }
    }
}
