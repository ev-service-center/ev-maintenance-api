using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EVServiceCenterMaintenanceAPI.Utils
{
    public static class JwtHelper
    {
        public static int GetUserIdFromHttpContext(HttpContext? context, ILogger? logger = null)
        {
            if (context == null)
            {
                logger?.LogError("HttpContext is null in JwtHelper.GetUserIdFromHttpContext.");
                throw new UnauthorizedAccessException("Bạn chưa đăng nhập.");
            }

            // Check if user is authenticated via middleware
            if (context.User != null && context.User.Identity != null && context.User.Identity.IsAuthenticated)
            {
                var userIdClaim = context.User.FindFirst("UserId")?.Value
                               ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var userId))
                {
                    logger?.LogInformation("Successfully retrieved UserId {UserId} from claims.", userId);
                    return userId;
                }
                logger?.LogError("Invalid UserId claim: {UserIdClaim}", userIdClaim);
                throw new UnauthorizedAccessException("ID người dùng không hợp lệ.");
            }

            // Fallback: Parse token from access_token query parameter
            var token = context.Request.Query["access_token"].ToString();
            if (string.IsNullOrWhiteSpace(token))
            {
                logger?.LogError("No access_token found in query parameters.");
                throw new UnauthorizedAccessException("Bạn chưa đăng nhập.");
            }

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);
                var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "UserId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    logger?.LogError("UserId or nameidentifier claim not found in token.");
                    throw new UnauthorizedAccessException("Bạn chưa đăng nhập.");
                }

                if (!int.TryParse(userIdClaim, out var userId))
                {
                    logger?.LogError("Invalid UserId format in token: {UserIdClaim}", userIdClaim);
                    throw new UnauthorizedAccessException($"ID người dùng không hợp lệ: {userIdClaim}");
                }

                logger?.LogInformation("Successfully retrieved UserId {UserId} from access_token.", userId);
                return userId;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to parse JWT token from access_token.");
                throw new UnauthorizedAccessException("Bạn chưa đăng nhập.");
            }
        }
    }
}