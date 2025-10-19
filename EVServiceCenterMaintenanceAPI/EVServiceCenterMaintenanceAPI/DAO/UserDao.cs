using EVServiceCenterMaintenanceAPI.Models;
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
            try
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                user.CreatedAt = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return user;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to create user.", ex);
            }
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

        public async Task<bool> IsEmailExists(string email)
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
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
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
                await transaction.CommitAsync();
                return existingUser;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to update user with ID {user.UserId}.", ex);
            }
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return false;

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to delete user with ID {userId}.", ex);
            }
        }
    }
}
