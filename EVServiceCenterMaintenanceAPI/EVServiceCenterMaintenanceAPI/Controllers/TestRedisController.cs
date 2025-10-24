using System.Text.Json;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestRedisController : ControllerBase
    {
        private readonly ITokenBlacklistService _blacklistService;
        private readonly ILogger<TestRedisController> _logger;
        private readonly IDistributedCache _cache;

        public TestRedisController(ITokenBlacklistService blacklistService, ILogger<TestRedisController> logger, IDistributedCache cache)
        {
            _blacklistService = blacklistService;
            _logger = logger;
            _cache = cache;
        }

        [HttpPost("test-blacklist")]
        public async Task<IActionResult> TestBlacklist()
        {
            try
            {
                var testJti = Guid.NewGuid().ToString();
                var expiresAt = DateTime.UtcNow.AddMinutes(1);

                // Test blacklist
                await _blacklistService.BlacklistTokenAsync(testJti, expiresAt);
                _logger.LogInformation("Token {Jti} blacklisted", testJti);

                // Test check blacklist
                var isBlacklisted = await _blacklistService.IsTokenBlacklistedAsync(testJti);
                _logger.LogInformation("Token {Jti} is blacklisted: {IsBlacklisted}", testJti, isBlacklisted);

                return Ok(new ApiResponse<object>(200, "Success", "Redis test successful", null, new
                {
                    TestJti = testJti,
                    IsBlacklisted = isBlacklisted,
                    ExpiresAt = expiresAt
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis test failed");
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Redis test failed: {ex.Message}"));
            }
        }

        [HttpGet("test-connection")]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                var testJti = "test-connection-" + DateTime.UtcNow.Ticks;
                var expiresAt = DateTime.UtcNow.AddMinutes(1);

                await _blacklistService.BlacklistTokenAsync(testJti, expiresAt);
                var isBlacklisted = await _blacklistService.IsTokenBlacklistedAsync(testJti);

                return Ok(new ApiResponse<object>(200, "Success", "Redis connection successful", null, new
                {
                    Message = "Redis is working!",
                    TestResult = isBlacklisted,
                    Timestamp = DateTime.UtcNow
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis connection test failed");
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Redis connection failed: {ex.Message}"));
            }
        }

        [HttpGet("user-tokens/{userId}")]
        public async Task<IActionResult> GetUserTokens(int userId)
        {
            try
            {
                var userTokensKey = $"user_tokens:{userId}";
                var tokensJson = await _cache.GetStringAsync(userTokensKey);

                if (string.IsNullOrEmpty(tokensJson))
                {
                    return NotFound(new ApiResponse<object>(404, "Not Found", $"No tokens found for user {userId}"));
                }

                var tokens = JsonSerializer.Deserialize<List<object>>(tokensJson);

                return Ok(new ApiResponse<object>(200, "Success", $"Tokens for user {userId}", null, new
                {
                    UserId = userId,
                    TotalTokens = tokens?.Count ?? 0,
                    Tokens = tokens
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user tokens");
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to get user tokens: {ex.Message}"));
            }
        }

    }
}
