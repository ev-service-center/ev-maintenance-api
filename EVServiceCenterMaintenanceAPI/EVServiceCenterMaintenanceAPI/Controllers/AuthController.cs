using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : Controller
    {
        private readonly UserDao _userDao;
        private readonly AuthDao _authDao;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;

        public AuthController(
            UserDao userDao,
            AuthDao authDao,
            IConfiguration configuration,
            EmailService emailService)
        {
            _userDao = userDao ?? throw new ArgumentNullException(nameof(userDao));
            _authDao = authDao ?? throw new ArgumentNullException(nameof(authDao));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto registerDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray());
                return BadRequest(new ApiResponse<object>(400, "Validation Error", "One or more validation errors occurred.", errors));
            }

            try
            {
                var existingUser = await _userDao.GetUserByEmailAsync(registerDto.Email);
                if (existingUser != null)
                    return BadRequest(new ApiResponse<object>(400, "Bad Request", $"Email '{registerDto.Email}' already in use."));

                var user = new User
                {
                    Username = registerDto.Email.Split('@')[0],
                    Email = registerDto.Email,
                    PasswordHash = HashPassword(registerDto.Password),
                    FullName = registerDto.FullName,
                    Role = UserRole.Customer.ToString(),
                    Status = UserStatus.Pending.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Avatar = DefaultAvatar.Local
                };
                var activationTokenResult = GenerateActivationToken();

                var authToken = new AuthToken
                {
                    TokenType = TokenType.Activation.ToString(),
                    TokenValue = activationTokenResult.Token,
                    ExpiresAt = activationTokenResult.ExpiresAt,
                    CreatedAt = DateTime.UtcNow,
                    IsUsed = false
                };

                var createdUser = await _userDao.RegisterUserAsync(user, registerDto.Password, authToken);

                var appUrl = _configuration["url"];
                var url = string.IsNullOrEmpty(appUrl) ? "https://localhost:3000" : appUrl;
                var activationLink = $"{url}/account/activate?userId={createdUser.UserId}&token={Uri.EscapeDataString(authToken.TokenValue)}";
                var emailSent = await _emailService.SendActivationEmailAsync(user.FullName, user.Email, activationLink, authToken.ExpiresAt);
                if (!emailSent)
                {
                    await _userDao.DeleteUserAsync(createdUser.UserId);
                    return StatusCode(500, new ApiResponse<object>(500, "Error", "Failed to send activation email. Please try again."));
                }

                return CreatedAtAction(nameof(ActivateAccount), new { userId = createdUser.UserId }, new ApiResponse<object>(
                    201,
                    "Created",
                    "Registration successful! Please check your email to activate your account within 24 hours.",
                    null,
                    new { UserId = createdUser.UserId, Message = "Registration successful.", ActivationRequired = true, ExpiresIn = "24 hours" }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to register: {ex.Message}"));
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray());
                return BadRequest(new ApiResponse<object>(400, "Validation Error", "One or more validation errors occurred.", errors));
            }

            try
            {
                var user = await _userDao.GetUserByEmailAsync(loginDto.Email);
                if (user == null || !VerifyPassword(loginDto.Password, user.PasswordHash))
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid email or password."));

                if (user.Status != UserStatus.Active.ToString())
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Account is not active. Please activate your account."));

                var deviceHash = HashDeviceInfo(Request.Headers["User-Agent"].ToString() + HttpContext.Connection.RemoteIpAddress?.ToString());
                var roles = new List<string> { user.Role };
                var accessToken = GenerateJwtToken(user, roles, deviceHash);
                var refreshTokenResult = GenerateRefreshTokenAsync(user);

                var authToken = new AuthToken
                {
                    UserId = user.UserId,
                    TokenType = TokenType.Refresh.ToString(),
                    TokenValue = refreshTokenResult.Token,
                    ExpiresAt = refreshTokenResult.ExpiresAt
                };
                await _authDao.CreateTokenAsync(authToken);

                return Ok(new ApiResponse<object>(200, "Success", "Login successful.", null, new
                {
                    AccessToken = accessToken.Token,
                    RefreshToken = authToken.TokenValue,
                    Roles = roles,
                    Username = user.Email,
                    FullName = user.FullName,
                    UserId = user.UserId
                }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to login: {ex.Message}"));
            }
        }

        [HttpPost("activate")]
        public async Task<IActionResult> ActivateAccount([FromBody] ActivateAccountRequestDto activateDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray());
                return BadRequest(new ApiResponse<object>(400, "Validation Error", "One or more validation errors occurred.", errors));
            }

            try
            {
                if (activateDto.UserId <= 0)
                    return BadRequest(new ApiResponse<object>(400, "Bad Request", "Invalid UserId."));

                var user = await _userDao.GetUserByIdAsync(activateDto.UserId);
                if (user == null)
                    return NotFound(new ApiResponse<object>(404, "Not Found", "User not found."));

                if (user.Status == UserStatus.Active.ToString())
                    return Ok(new ApiResponse<object>(200, "Success", "Account already activated."));

                if (user.Status != UserStatus.Pending.ToString())
                    return BadRequest(new ApiResponse<object>(400, "Bad Request", "Account is not in pending state."));

                var activationToken = await _authDao.GetValidTokenByValueAndTypeAsync(activateDto.Token, TokenType.Activation.ToString());
                if (activationToken == null || activationToken.UserId != user.UserId)
                    return BadRequest(new ApiResponse<object>(400, "Bad Request", "Invalid or expired activation token."));

                user.Status = UserStatus.Active.ToString();
                user.UpdatedAt = DateTime.UtcNow;

                await _authDao.MarkTokenAsUsedAsync(activateDto.Token, TokenType.Activation.ToString());

                await _userDao.UpdateUserAsync(user);

                return Ok(new ApiResponse<object>(200, "Success", "Account activated successfully!", null, new
                {
                    Message = "Account activated successfully",
                    UserId = user.UserId,
                    Email = user.Email,
                    Status = "active"
                }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to activate account: {ex.Message}"));
            }
        }
        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        private bool VerifyPassword(string password, string hash)
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }

        private JwtTokenResult GenerateJwtToken(User user, List<string> roles, string deviceHash)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim("UserId", user.UserId.ToString()),
                new Claim("DeviceHash", deviceHash)
            };
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? throw new Exception("Jwt:Key is missing")));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddMinutes(double.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60"));

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds);

            return new JwtTokenResult
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresAt = expires
            };
        }

        private JwtTokenResult GenerateActivationToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            var token = Convert.ToBase64String(randomBytes).Replace("=", "").Replace("+", "-").Replace("/", "_");
            var expires = DateTime.UtcNow.AddHours(24);
            return new JwtTokenResult { Token = token, ExpiresAt = expires };
        }

        private JwtTokenResult GenerateRefreshTokenAsync(User user)
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            var refreshToken = Convert.ToBase64String(randomBytes);
            var expires = DateTime.UtcNow.AddDays(double.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7"));
            return new JwtTokenResult { Token = refreshToken, ExpiresAt = expires };
        }

        private string HashDeviceInfo(string info)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(info ?? "");
            var hashBytes = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hashBytes);
        }
    }
    public class JwtTokenResult
    {
        public string Token { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
    }
}
