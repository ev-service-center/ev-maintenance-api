using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class AuthTokenDao
    {
        private readonly EvserviceCenterDbContext _context;

        public AuthTokenDao(EvserviceCenterDbContext context)
        {
            _context = context;
        }
        public async Task<AuthToken?> GetAuthTokenByIdAsync(int tokenId)
        {
            return await _context.AuthTokens.FirstOrDefaultAsync(t => t.TokenId == tokenId);
        }

        public async Task<AuthToken> UpdateAuthTokenAsync(AuthToken token)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingToken = await _context.AuthTokens.FirstOrDefaultAsync(t => t.TokenId == token.TokenId);
                if (existingToken == null)
                    throw new Exception($"Auth token with ID {token.TokenId} not found.");

                existingToken.TokenValue = token.TokenValue;
                existingToken.ExpiresAt = token.ExpiresAt;
                existingToken.IsUsed = token.IsUsed;
                existingToken.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return existingToken;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to update auth token with ID {token.TokenId}.", ex);
            }
        }
    }
}
