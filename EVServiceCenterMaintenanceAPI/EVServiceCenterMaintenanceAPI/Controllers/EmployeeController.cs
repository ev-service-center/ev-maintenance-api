using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
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
                var createdDto = new EmployeeResponseDto
                {
                    EmployeeId = createdEmployee.EmployeeId,
                    CenterId = createdEmployee.CenterId,
                    Shift = createdEmployee.Shift,
                    PerformanceScore = createdEmployee.PerformanceScore ?? 0m,
                    Certificate = createdEmployee.Certificate,
                    CreatedAt = createdEmployee.CreatedAt,
                    UpdatedAt = createdEmployee.UpdatedAt
                };

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
                    queryParams.CenterId = currentEmployee.CenterId;
                }

                var (employees, total) = await _employeeDao.GetAllEmployeesAsync(queryParams);

                var dtos = employees.Select(e => new EmployeeResponseDto
                {
                    EmployeeId = e.EmployeeId,
                    CenterId = e.CenterId,
                    Shift = e.Shift,
                    PerformanceScore = e.PerformanceScore ?? 0m,
                    Certificate = e.Certificate,
                    CreatedAt = e.CreatedAt,
                    UpdatedAt = e.UpdatedAt
                }).ToList();

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
    }
}
