using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class AuthDao
    {
        private readonly EvserviceCenterDbContext _context;

        public AuthDao(EvserviceCenterDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<AuthToken> CreateTokenAsync(AuthToken token)
        {
            if (token == null)
                throw new ArgumentNullException(nameof(token));

            token.CreatedAt = DateTime.UtcNow;
            token.UpdatedAt = DateTime.UtcNow;
            token.IsUsed = false;

            _context.AuthTokens.Add(token);
            await _context.SaveChangesAsync();
            return token;
        }

        public async Task<AuthToken?> GetValidTokenByValueAndTypeAsync(string tokenValue, string tokenType)
        {
            if (string.IsNullOrEmpty(tokenValue) || string.IsNullOrEmpty(tokenType))
                throw new ArgumentException("TokenValue and TokenType cannot be null or empty.");

            return await _context.AuthTokens
                .FirstOrDefaultAsync(t =>
                    t.TokenValue == tokenValue &&
                    t.TokenType == tokenType &&
                    !t.IsUsed &&
                    t.ExpiresAt > DateTime.UtcNow);
        }

        public async Task UpdateTokenAsync(AuthToken token)
        {
            if (token == null)
                throw new ArgumentNullException(nameof(token));

            token.UpdatedAt = DateTime.UtcNow;
            _context.AuthTokens.Update(token);
            await _context.SaveChangesAsync();
        }

        public async Task MarkTokenAsUsedAsync(string tokenValue, string tokenType)
        {
            var token = await GetValidTokenByValueAndTypeAsync(tokenValue, tokenType);
            if (token != null)
            {
                token.IsUsed = true;
                token.UpdatedAt = DateTime.UtcNow;
                await UpdateTokenAsync(token);
            }
        }
    }
}