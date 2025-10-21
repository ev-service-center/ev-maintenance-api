using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Services;
using EVServiceCenterMaintenanceAPI.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : Controller
    {
        private readonly UserDao _userDao;
        private readonly ImageService _imageService;
        private readonly EmailService _emailService;

        public UserController(UserDao userDao, ImageService imageService, EmailService emailService)
        {
            _userDao = userDao;
            _imageService = imageService;
            _emailService = emailService;
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
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Invalid input data."));
                }

                var randomPassword = GenerateRandomPassword();
                var user = new User
                {
                    Username = userDto.Username,
                    FullName = userDto.FullName,
                    Email = userDto.Email,
                    Phone = userDto.Phone,
                    Role = userDto.Role.ToString(),
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
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
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
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
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
                password[i] = validChars[randomBytes[i] % 72];
            }
            return new string(password);
        }
    }
}
