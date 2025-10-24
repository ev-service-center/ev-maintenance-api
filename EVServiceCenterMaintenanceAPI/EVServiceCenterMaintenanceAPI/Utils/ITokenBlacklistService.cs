namespace EVServiceCenterMaintenanceAPI.Utils
{
    public interface ITokenBlacklistService
    {
        Task BlacklistTokenAsync(string jti, DateTime expiresAt);

        Task<bool> IsTokenBlacklistedAsync(string jti);

        Task BlacklistAllUserTokensAsync(int userId);

        Task RemoveFromBlacklistAsync(string jti);

        Task TrackUserTokenAsync(int userId, string jti, DateTime expiresAt);
    }
}
