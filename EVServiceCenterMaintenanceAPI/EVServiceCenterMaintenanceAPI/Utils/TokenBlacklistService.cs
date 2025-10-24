using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace EVServiceCenterMaintenanceAPI.Utils
{
    public class TokenBlacklistService : ITokenBlacklistService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<TokenBlacklistService> _logger;
        private const string BLACKLIST_PREFIX = "blacklist:";
        private const string USER_TOKENS_PREFIX = "user_tokens:";

        public TokenBlacklistService(IDistributedCache cache, ILogger<TokenBlacklistService> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task BlacklistTokenAsync(string jti, DateTime expiresAt)
        {
            if (string.IsNullOrEmpty(jti))
                throw new ArgumentException("JTI cannot be null or empty", nameof(jti));

            try
            {
                var blacklistKey = $"{BLACKLIST_PREFIX}{jti}";
                var blacklistData = new
                {
                    Jti = jti,
                    BlacklistedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt
                };

                var jsonData = JsonSerializer.Serialize(blacklistData);
                var cacheExpiry = expiresAt - DateTime.UtcNow;
                if (cacheExpiry > TimeSpan.Zero)
                {
                    var options = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpiration = expiresAt
                    };

                    await _cache.SetStringAsync(blacklistKey, jsonData, options);
                    _logger.LogInformation("Token {Jti} has been blacklisted until {ExpiresAt}", jti, expiresAt);
                }
                else
                {
                    _logger.LogWarning("Token {Jti} has already expired, skipping blacklist", jti);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to blacklist token {Jti}", jti);
                throw;
            }
        }

        public async Task<bool> IsTokenBlacklistedAsync(string jti)
        {
            if (string.IsNullOrEmpty(jti))
                return false;

            try
            {
                var blacklistKey = $"{BLACKLIST_PREFIX}{jti}";
                var blacklistData = await _cache.GetStringAsync(blacklistKey);

                if (string.IsNullOrEmpty(blacklistData))
                {
                    return false;
                }
                _logger.LogDebug("Token {Jti} is blacklisted", jti);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check blacklist status for token {Jti}", jti);
                return false;
            }
        }

        public async Task BlacklistAllUserTokensAsync(int userId)
        {
            try
            {
                var userTokensKey = $"{USER_TOKENS_PREFIX}{userId}";
                var existingTokens = await _cache.GetStringAsync(userTokensKey);

                if (!string.IsNullOrEmpty(existingTokens))
                {
                    var tokenList = JsonSerializer.Deserialize<TokenInfo[]>(existingTokens);
                    if (tokenList != null)
                    {
                        foreach (var tokenInfo in tokenList)
                        {
                            await BlacklistTokenAsync(tokenInfo.Jti, tokenInfo.ExpiresAt);
                        }
                    }
                }
                await _cache.RemoveAsync(userTokensKey);
                _logger.LogInformation("All tokens for user {UserId} have been blacklisted", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to blacklist all tokens for user {UserId}", userId);
                throw;
            }
        }

        public async Task RemoveFromBlacklistAsync(string jti)
        {
            if (string.IsNullOrEmpty(jti))
                return;

            try
            {
                var blacklistKey = $"{BLACKLIST_PREFIX}{jti}";
                await _cache.RemoveAsync(blacklistKey);
                _logger.LogInformation("Token {Jti} has been removed from blacklist", jti);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove token {Jti} from blacklist", jti);
                throw;
            }
        }

        public async Task TrackUserTokenAsync(int userId, string jti, DateTime expiresAt)
        {
            try
            {
                var userTokensKey = $"{USER_TOKENS_PREFIX}{userId}";
                var existingTokens = await _cache.GetStringAsync(userTokensKey);

                var newTokenInfo = new TokenInfo { Jti = jti, ExpiresAt = expiresAt, CreatedAt = DateTime.UtcNow };

                List<TokenInfo> tokenList;
                if (string.IsNullOrEmpty(existingTokens))
                {
                    tokenList = new List<TokenInfo> { newTokenInfo };
                }
                else
                {
                    var existingList = JsonSerializer.Deserialize<List<TokenInfo>>(existingTokens);
                    tokenList = existingList ?? new List<TokenInfo>();
                    tokenList.Add(newTokenInfo);
                }

                var jsonData = JsonSerializer.Serialize(tokenList);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = DateTime.UtcNow.AddDays(7)
                };

                await _cache.SetStringAsync(userTokensKey, jsonData, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to track token {Jti} for user {UserId}", jti, userId);
            }
        }

        private class TokenInfo
        {
            public string Jti { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}