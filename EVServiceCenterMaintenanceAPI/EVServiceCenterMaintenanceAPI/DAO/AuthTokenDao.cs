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
    }
}
