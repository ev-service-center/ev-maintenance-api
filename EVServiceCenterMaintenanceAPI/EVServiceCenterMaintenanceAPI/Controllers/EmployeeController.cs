using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
using EVServiceCenterMaintenanceAPI.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeeController : Controller
    {
        private readonly EmployeeDao _employeeDao;
        private readonly UserDao _userDao;
        private readonly ServiceCenterDao _centerDao;

        public EmployeeController(EmployeeDao employeeDao, UserDao userDao, ServiceCenterDao centerDao)
        {
            _employeeDao = employeeDao;
            _userDao = userDao;
            _centerDao = centerDao;
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateEmployee([FromBody] EmployeeCreateRequestDto dto)
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
                var user = await _userDao.GetUserByIdAsync(dto.EmployeeId);
                if (user == null)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", $"User with ID {dto.EmployeeId} not found."));
                var userRole = Enum.Parse<UserRole>(user.Role);
                if (userRole != UserRole.Staff && userRole != UserRole.Technician)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Employee can only be created for Staff or Technician users."));
                var existing = await _employeeDao.GetEmployeeByIdAsync(dto.EmployeeId);
                if (existing != null)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Employee already exists for this user."));
                var centerExists = await _centerDao.IsExistServiceCenterAsync(dto.CenterId);
                if (!centerExists)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", $"Service Center with ID {dto.CenterId} not found."));

                var employee = new Employee
                {
                    EmployeeId = dto.EmployeeId,
                    CenterId = dto.CenterId,
                    Shift = dto.Shift,
                    PerformanceScore = dto.PerformanceScore,
                    Certificate = dto.Certificate
                };

                var createdEmployee = await _employeeDao.CreateEmployeeAsync(employee);
                // Load User info
                var loadedEmployee = await _employeeDao.GetEmployeeByIdAsync(createdEmployee.EmployeeId);
                if (loadedEmployee == null)
                    return StatusCode(500, new ApiResponse<object>(500, "Error", "Failed to load created employee."));
                var createdDto = MapEmployeeToDto(loadedEmployee);

                return CreatedAtAction(nameof(GetEmployeeById), new { id = createdEmployee.EmployeeId }, new ApiResponse<EmployeeResponseDto>(201, "Created", "Employee created successfully.", data: createdDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> GetEmployeeById(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst("UserId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                var currentUser = await _userDao.GetUserByIdAsync(currentUserId);
                if (currentUser == null)
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "User not found."));

                var employee = await _employeeDao.GetEmployeeByIdAsync(id);
                if (employee == null)
                    return NotFound(new ApiResponse<EmployeeResponseDto>(404, "NotFound", "Employee not found."));

                if (currentUser.Role == UserRole.Staff.ToString())
                {
                    var currentEmployee = await _employeeDao.GetEmployeeByIdAsync(currentUserId);
                    if (currentEmployee == null)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", "Staff user does not have an associated employee record."));

                    if (currentEmployee.CenterId != employee.CenterId)
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden", "Staff can only view employees in their own service center."));
                }

                var dto = MapEmployeeToDto(employee);

                return Ok(new ApiResponse<EmployeeResponseDto>(200, "Success", "Employee retrieved successfully.", data: dto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> GetAllEmployees([FromQuery] EmployeeQueryParams queryParams)
        {
            try
            {
                var userIdClaim = User.FindFirst("UserId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                var currentUser = await _userDao.GetUserByIdAsync(currentUserId);
                if (currentUser == null)
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "User not found."));

                if (currentUser.Role == UserRole.Staff.ToString())
                {
                    var currentEmployee = await _employeeDao.GetEmployeeByIdAsync(currentUserId);
                    if (currentEmployee == null)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", "Staff user does not have an associated employee record."));
                    if (queryParams.CenterId.HasValue && queryParams.CenterId.Value != currentEmployee.CenterId)
                    {
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                            $"Staff can only query employees from their own service center (Center ID: {currentEmployee.CenterId})."));
                    }
                    queryParams.CenterId = currentEmployee.CenterId;
                }

                var (employees, total) = await _employeeDao.GetAllEmployeesAsync(queryParams);

                var dtos = employees.Select(e => MapEmployeeToDto(e)).ToList();

                var responseData = new { employees = dtos, total, page = queryParams.Page, pageSize = queryParams.PageSize };
                return Ok(new ApiResponse<object>(200, "Success", "Employees retrieved successfully.", data: responseData));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<object>(400, "BadRequest", ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        private EmployeeResponseDto MapEmployeeToDto(Employee employee)
        {
            var dto = new EmployeeResponseDto
            {
                EmployeeId = employee.EmployeeId,
                CenterId = employee.CenterId,
                Shift = employee.Shift,
                PerformanceScore = employee.PerformanceScore ?? 0m,
                Certificate = employee.Certificate,
                CreatedAt = employee.CreatedAt,
                UpdatedAt = employee.UpdatedAt
            };

            // Map User info if available
            if (employee.EmployeeNavigation != null)
            {
                dto.User = new EmployeeUserInfoDto
                {
                    UserId = employee.EmployeeNavigation.UserId,
                    Username = employee.EmployeeNavigation.Username,
                    FullName = employee.EmployeeNavigation.FullName,
                    Email = employee.EmployeeNavigation.Email,
                    Phone = employee.EmployeeNavigation.Phone,
                    Role = Enum.Parse<UserRole>(employee.EmployeeNavigation.Role),
                    Status = Enum.Parse<UserStatus>(employee.EmployeeNavigation.Status),
                    Avatar = employee.EmployeeNavigation.Avatar != null
                        ? HttpContext.ToAbsoluteUrl(employee.EmployeeNavigation.Avatar)
                        : HttpContext.ToAbsoluteUrl(DefaultAvatar.Local)
                };
            }

            return dto;
        }
    }
}
