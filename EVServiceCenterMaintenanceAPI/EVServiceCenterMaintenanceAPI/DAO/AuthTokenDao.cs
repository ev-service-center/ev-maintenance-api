using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
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

        public async Task<AuthToken> CreateAuthTokenAsync(AuthToken token)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                token.CreatedAt = DateTime.UtcNow;
                token.UpdatedAt = DateTime.UtcNow;
                _context.AuthTokens.Add(token);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return token;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("Failed to create auth token.", ex);
            }
        }

        public async Task<(List<AuthToken> Tokens, int Total)> GetAllAuthTokensAsync(AuthTokenQueryParams queryParams)
        {
            var validation = queryParams.Validate();
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage);
            }

            var query = _context.AuthTokens.AsQueryable();
            if (queryParams.TokenType.HasValue)
                query = query.Where(t => t.TokenType == queryParams.TokenType.ToString());
            if (queryParams.UserId.HasValue)
                query = query.Where(t => t.UserId == queryParams.UserId.Value);
            if (queryParams.FromDate.HasValue)
                query = query.Where(t => t.CreatedAt >= queryParams.FromDate.Value);
            if (queryParams.ToDate.HasValue)
                query = query.Where(t => t.CreatedAt <= queryParams.ToDate.Value);

            if (!string.IsNullOrEmpty(queryParams.SortBy))
            {
                bool isAscending = queryParams.SortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);
                switch (queryParams.SortBy.ToLower())
                {
                    case "tokentype":
                        query = isAscending ? query.OrderBy(t => t.TokenType) : query.OrderByDescending(t => t.TokenType);
                        break;
                    case "createdat":
                        query = isAscending ? query.OrderBy(t => t.CreatedAt) : query.OrderByDescending(t => t.CreatedAt);
                        break;
                    default:
                        query = isAscending ? query.OrderBy(t => t.TokenId) : query.OrderByDescending(t => t.TokenId);
                        break;
                }
            }

            var total = await query.CountAsync();
            var tokens = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (tokens, total);
        }
    }
}
