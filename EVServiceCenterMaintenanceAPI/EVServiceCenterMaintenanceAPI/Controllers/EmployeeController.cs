using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
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

                return CreatedAtAction(nameof(GetEmployee), new { id = createdEmployee.EmployeeId }, new ApiResponse<EmployeeResponseDto>(201, "Created", "Employee created successfully.", data: createdDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> GetEmployee(int id)
        {
            try
            {
                var employee = await _employeeDao.GetEmployeeByIdAsync(id);
                if (employee == null)
                    return NotFound(new ApiResponse<EmployeeResponseDto>(404, "NotFound", "Employee not found."));

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
    }
}
