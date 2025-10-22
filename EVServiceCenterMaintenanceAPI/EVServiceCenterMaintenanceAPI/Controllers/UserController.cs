using System.Security.Cryptography;
using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
using EVServiceCenterMaintenanceAPI.Services;
using EVServiceCenterMaintenanceAPI.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : Controller
    {
        private readonly UserDao _userDao;
        private readonly ImageService _imageService;
        private readonly EmailService _emailService;

        private readonly AuthDao _authDao;

        public UserController(UserDao userDao, ImageService imageService, EmailService emailService, AuthDao authDao)
        {
            _userDao = userDao;
            _imageService = imageService;
            _emailService = emailService;
            _authDao = authDao;
        }

        [HttpPost]
        [Authorize(Roles = "Admin, Staff")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateUser([FromForm] UserCreateRequestDto userDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(kvp => !string.IsNullOrEmpty(kvp.Key) && kvp.Key != "id" && kvp.Value?.Errors?.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? []);
                    return BadRequest(new ApiResponse<object>(400, "Validation Error", "One or more validation errors occurred.", errors));
                }

                // Check if username or email already exists
                bool isUsernameExists = await _userDao.IsUsernameExistsAsync(userDto.Username);
                bool isEmailExists = await _userDao.IsEmailExistsAsync(userDto.Email);

                if (isUsernameExists)
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Username already exists."));
                }

                if (isEmailExists)
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Email already exists."));
                }

                if (userDto.Role.HasValue && !Enum.IsDefined(typeof(UserRole), userDto.Role.Value))
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Invalid role."));
                }

                var randomPassword = GenerateRandomPassword();
                var user = new User
                {
                    Username = userDto.Username,
                    FullName = userDto.FullName,
                    Email = userDto.Email,
                    Phone = userDto.Phone,
                    Role = userDto.Role!.Value.ToString(),
                    Status = UserStatus.Active.ToString(),
                    Avatar = userDto.Avatar != null ? await _imageService.SaveImageAsync(userDto.Avatar) : DefaultAvatar.Local,
                };

                var createdUser = await _userDao.CreateUserAsync(user, randomPassword);
                var createdUserDto = new UserResponseDto
                {
                    UserId = createdUser.UserId,
                    Username = createdUser.Username,
                    FullName = createdUser.FullName,
                    Email = createdUser.Email,
                    Phone = createdUser.Phone,
                    Role = Enum.Parse<UserRole>(createdUser.Role),
                    Status = Enum.Parse<UserStatus>(createdUser.Status),
                    Avatar = UrlHelper.ToAbsoluteUrl(HttpContext, createdUser.Avatar ?? DefaultAvatar.Local),
                    CreatedAt = createdUser.CreatedAt,
                    UpdatedAt = createdUser.UpdatedAt
                };

                TaskHelper.FireAndForget(createdUserDto, randomPassword, createdUser.Email, _emailService.SendUserCreatedEmailAsync);

                return CreatedAtAction(nameof(GetUser), new { id = createdUser.UserId },
                    new ApiResponse<UserResponseDto>(201, "Created", "User created successfully.", data: createdUserDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to create user: {ex.Message}"));
            }
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetUser(int id)
        {
            try
            {
                var user = await _userDao.GetUserByIdAsync(id);
                if (user == null)
                    return NotFound(new ApiResponse<UserResponseDto>(404, "NotFound", "User not found."));

                var userDto = new UserResponseDto
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    FullName = user.FullName,
                    Email = user.Email,
                    Phone = user.Phone,
                    Role = Enum.Parse<UserRole>(user.Role),
                    Status = Enum.Parse<UserStatus>(user.Status),
                    Avatar = UrlHelper.ToAbsoluteUrl(HttpContext, user.Avatar ?? DefaultAvatar.Local),
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt
                };

                return Ok(new ApiResponse<UserResponseDto>(200, "Success", "User retrieved successfully.", data: userDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to get user: {ex.Message}"));
            }
        }

        [HttpGet]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> GetAllUsers([FromQuery] UserQueryParams queryParams)
        {
            try
            {
                //tuple type
                var (users, total) = await _userDao.GetAllUsersAsync(queryParams);

                var userDtos = users.Select(u => new UserResponseDto
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    FullName = u.FullName,
                    Email = u.Email,
                    Phone = u.Phone,
                    Role = Enum.Parse<UserRole>(u.Role),
                    Status = Enum.Parse<UserStatus>(u.Status),
                    Avatar = UrlHelper.ToAbsoluteUrl(HttpContext, u.Avatar ?? DefaultAvatar.Local),
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt
                }).ToList();

                var responseData = new { users = userDtos, total, page = queryParams.Page, pageSize = queryParams.PageSize };
                return Ok(new ApiResponse<object>(200, "Success", "Users retrieved successfully.", data: responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to get users: {ex.Message}"));
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateUser(int id, [FromForm] UserUpdateRequestDto userDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(kvp => !string.IsNullOrEmpty(kvp.Key) && kvp.Key != "id" && kvp.Value?.Errors?.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? []);
                    return BadRequest(new ApiResponse<object>(400, "Validation Error", "One or more validation errors occurred.", errors));
                }

                var existingUser = await _userDao.GetUserByIdAsync(id);
                if (existingUser == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "User not found."));

                // Check if email is being changed and if new email already exists
                if (userDto.Email != null && existingUser.Email != userDto.Email)
                {
                    bool isEmailExists = await _userDao.IsEmailExistsAsync(userDto.Email);
                    if (isEmailExists)
                    {
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", "Email already exists."));
                    }
                }

                if (userDto.Role.HasValue && !Enum.IsDefined(typeof(UserRole), userDto.Role.Value))
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Invalid role."));
                }

                if (userDto.Status.HasValue && !Enum.IsDefined(typeof(UserStatus), userDto.Status.Value))
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Invalid status."));
                }

                string? oldAvatarPath = existingUser.Avatar;

                if (userDto.FullName != null)
                    existingUser.FullName = userDto.FullName;

                if (userDto.Email != null)
                    existingUser.Email = userDto.Email;

                if (userDto.Phone != null)
                    existingUser.Phone = userDto.Phone;

                if (userDto.Role.HasValue)
                    existingUser.Role = userDto.Role.Value.ToString();

                if (userDto.Status.HasValue)
                    existingUser.Status = userDto.Status.Value.ToString();

                if (userDto.Avatar != null)
                    existingUser.Avatar = await _imageService.SaveImageAsync(userDto.Avatar);

                existingUser.UpdatedAt = DateTime.UtcNow;

                var updatedUser = await _userDao.UpdateUserAsync(existingUser);
                var updatedUserDto = new UserResponseDto
                {
                    UserId = updatedUser.UserId,
                    Username = updatedUser.Username,
                    FullName = updatedUser.FullName,
                    Email = updatedUser.Email,
                    Phone = updatedUser.Phone,
                    Role = Enum.Parse<UserRole>(updatedUser.Role),
                    Status = Enum.Parse<UserStatus>(updatedUser.Status),
                    Avatar = UrlHelper.ToAbsoluteUrl(HttpContext, updatedUser.Avatar ?? DefaultAvatar.Local),
                    CreatedAt = updatedUser.CreatedAt,
                    UpdatedAt = updatedUser.UpdatedAt
                };

                // Delete old avatar
                if (userDto.Avatar != null && !string.IsNullOrEmpty(oldAvatarPath)
                    && oldAvatarPath != DefaultAvatar.Local)
                {
                    _imageService.DeleteImage(oldAvatarPath);
                }

                return Ok(new ApiResponse<UserResponseDto>(200, "Success", "User updated successfully.", data: updatedUserDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to update user: {ex.Message}"));
            }
        }

        [HttpPut("profile")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateProfile([FromForm] UserProfileUpdateRequestDto userDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(kvp => !string.IsNullOrEmpty(kvp.Key) && kvp.Key != "id" && kvp.Value?.Errors?.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? []);
                    return BadRequest(new ApiResponse<object>(400, "Validation Error", "One or more validation errors occurred.", errors));
                }

                var userId = JwtHelper.GetUserIdFromHttpContext(HttpContext);

                var existingUser = await _userDao.GetUserByIdAsync(userId);
                if (existingUser == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "User not found."));

                // Check if username is being changed and if new username already exists
                if (userDto.Username != null && existingUser.Username != userDto.Username)
                {
                    bool isUsernameExists = await _userDao.IsUsernameExistsAsync(userDto.Username);
                    if (isUsernameExists)
                    {
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", "Username already exists."));
                    }
                }

                // Check if email is being changed and if new email already exists
                if (userDto.Email != null && existingUser.Email != userDto.Email)
                {
                    bool isEmailExists = await _userDao.IsEmailExistsAsync(userDto.Email);
                    if (isEmailExists)
                    {
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", "Email already exists."));
                    }
                }

                string? oldAvatarPath = existingUser.Avatar;

                if (userDto.Username != null)
                    existingUser.Username = userDto.Username;

                if (userDto.FullName != null)
                    existingUser.FullName = userDto.FullName;

                if (userDto.Email != null)
                    existingUser.Email = userDto.Email;

                if (userDto.Phone != null)
                    existingUser.Phone = userDto.Phone;

                if (userDto.Avatar != null)
                    existingUser.Avatar = await _imageService.SaveImageAsync(userDto.Avatar);

                existingUser.UpdatedAt = DateTime.UtcNow;

                var updatedUser = await _userDao.UpdateUserAsync(existingUser);
                var updatedUserDto = new UserResponseDto
                {
                    UserId = updatedUser.UserId,
                    Username = updatedUser.Username,
                    FullName = updatedUser.FullName,
                    Email = updatedUser.Email,
                    Phone = updatedUser.Phone,
                    Role = Enum.Parse<UserRole>(updatedUser.Role),
                    Status = Enum.Parse<UserStatus>(updatedUser.Status),
                    Avatar = UrlHelper.ToAbsoluteUrl(HttpContext, updatedUser.Avatar ?? DefaultAvatar.Local),
                    CreatedAt = updatedUser.CreatedAt,
                    UpdatedAt = updatedUser.UpdatedAt
                };

                // Delete old avatar
                if (userDto.Avatar != null && !string.IsNullOrEmpty(oldAvatarPath)
                    && oldAvatarPath != DefaultAvatar.Local)
                {
                    _imageService.DeleteImage(oldAvatarPath);
                }

                return Ok(new ApiResponse<UserResponseDto>(200, "Success", "Profile updated successfully.", data: updatedUserDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to update profile: {ex.Message}"));
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var user = await _userDao.GetUserByIdAsync(id);
                if (user == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "User not found."));

                var success = await _userDao.DeleteUserAsync(id);
                if (!success)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "User not found."));

                if (!string.IsNullOrEmpty(user.Avatar))
                    _imageService.DeleteImage(user.Avatar);

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to delete user: {ex.Message}"));
            }
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto changePasswordDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                        .Where(kvp => !string.IsNullOrEmpty(kvp.Key) && kvp.Key != "id" && kvp.Value?.Errors?.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? []);
                return BadRequest(new ApiResponse<object>(400, "Validation Error", "One or more validation errors occurred.", errors));
            }

            try
            {
                var userId = JwtHelper.GetUserIdFromHttpContext(HttpContext);

                // Validate password strength
                var (isValid, errorMessage) = ValidationHelper.ValidatePasswordStrength(changePasswordDto.NewPassword);
                if (!isValid)
                {
                    return BadRequest(new ApiResponse<object>(400, "Bad Request", errorMessage));
                }

                await _userDao.UpdatePasswordAsync(userId, changePasswordDto.OldPassword, changePasswordDto.NewPassword);

                // Revoke all refresh tokens for security
                await _authDao.RevokeRefreshTokensByUserIdAsync(userId);

                return Ok(new ApiResponse<object>(200, "Success", "Password changed successfully. Please login again with your new password."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to change password: {ex.Message}"));
            }
        }

        //Helper Function
        private static string GenerateRandomPassword(int length = 12)
        {
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*";
            char[] password = new char[length];
            byte[] randomBytes = new byte[length];
            using var rdb = RandomNumberGenerator.Create();
            rdb.GetBytes(randomBytes);
            for (int i = 0; i < length; i++)
            {
                password[i] = validChars[randomBytes[i] % validChars.Length];
            }
            return new string(password);
        }
    }
}
