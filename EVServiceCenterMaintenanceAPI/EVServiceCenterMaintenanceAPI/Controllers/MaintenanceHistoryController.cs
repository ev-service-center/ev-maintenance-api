using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MaintenanceHistoryController : ControllerBase
    {
        private readonly MaintenanceHistoryDao _maintenanceHistoryDao;
        private readonly EvserviceCenterDbContext _context;
        private readonly EmployeeDao _employeeDao;

        public MaintenanceHistoryController(MaintenanceHistoryDao maintenanceHistoryDao, EvserviceCenterDbContext context, EmployeeDao employeeDao)
        {
            _maintenanceHistoryDao = maintenanceHistoryDao;
            _context = context;
            _employeeDao = employeeDao;
        }

        #region Helper Methods

        /// <summary>
        /// Get current user info from claims
        /// </summary>
        private (string? UserIdClaim, string? UserRole) GetCurrentUserInfo()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            return (userIdClaim, userRole);
        }

        /// <summary>
        /// Validate Staff/Technician can only access maintenance histories at their center
        /// </summary>
        private async Task<IActionResult?> ValidateCenterAccessAsync(int? historyCenterId, string? userRole, int currentUserId)
        {
            if (userRole != UserRole.Staff.ToString() && userRole != UserRole.Technician.ToString())
                return null; // Not Staff/Technician, no restriction

            var currentEmployee = await _employeeDao.GetEmployeeByIdAsync(currentUserId);
            if (currentEmployee == null)
                return BadRequest(new ApiResponse<object>(400, "BadRequest",
                    $"{userRole} user does not have an associated employee record."));

            if (historyCenterId.HasValue && historyCenterId.Value != currentEmployee.CenterId)
                return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                    $"{userRole} can only access maintenance histories from their own service center (Center ID: {currentEmployee.CenterId})."));

            return null;
        }

        /// <summary>
        /// Get current employee for center validation
        /// </summary>
        private async Task<(Employee? Employee, IActionResult? Error)> GetCurrentEmployeeAsync(int currentUserId, string? userRole)
        {
            var currentEmployee = await _employeeDao.GetEmployeeByIdAsync(currentUserId);
            if (currentEmployee == null)
                return (null, BadRequest(new ApiResponse<object>(400, "BadRequest",
                    $"{userRole} user does not have an associated employee record.")));

            return (currentEmployee, null);
        }

        #endregion

        [HttpPost]
        [Authorize(Roles = "Technician,Admin")]
        public async Task<IActionResult> CreateMaintenanceHistory([FromBody] MaintenanceHistoryCreateRequestDto dto)
        {
            try
            {
                // Get current user info for authorization
                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                // Center restriction: Staff/Technician chỉ tạo maintenance history tại center của họ
                if (userRole == UserRole.Staff.ToString() || userRole == UserRole.Technician.ToString())
                {
                    // Get vehicle to check center
                    var vehicle = await _context.Vehicles
                        .Include(v => v.WorkOrders)
                        .FirstOrDefaultAsync(v => v.VehicleId == dto.VehicleId);

                    if (vehicle?.WorkOrders?.Any() == true)
                    {
                        var lastWorkOrder = vehicle.WorkOrders.OrderByDescending(wo => wo.CheckInAt).FirstOrDefault();
                        if (lastWorkOrder != null)
                        {
                            var centerAccessError = await ValidateCenterAccessAsync(lastWorkOrder.CenterId, userRole, currentUserId);
                            if (centerAccessError != null) return centerAccessError;
                        }
                    }
                }

                var history = new MaintenanceHistory
                {
                    VehicleId = dto.VehicleId,
                    MaintenanceDate = dto.MaintenanceDate,
                    Description = dto.Description,
                    Notes = dto.Notes,
                    Cost = dto.Cost,
                    MileageAtMaintenance = dto.MileageAtMaintenance
                };

                var createdHistory = await _maintenanceHistoryDao.CreateMaintenanceHistoryAsync(history);
                var createdDto = new MaintenanceHistoryResponseDto
                {
                    HistoryId = createdHistory.HistoryId,
                    VehicleId = createdHistory.VehicleId,
                    MaintenanceDate = createdHistory.MaintenanceDate,
                    Description = createdHistory.Description,
                    Notes = createdHistory.Notes,
                    Cost = createdHistory.Cost,
                    MileageAtMaintenance = createdHistory.MileageAtMaintenance,
                    CreatedAt = createdHistory.CreatedAt,
                    UpdatedAt = createdHistory.UpdatedAt
                };

                return CreatedAtAction(nameof(GetMaintenanceHistory), new { id = createdHistory.HistoryId }, new ApiResponse<MaintenanceHistoryResponseDto>(201, "Created", "Maintenance history created successfully.", data: createdDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Customer,Staff,Technician,Admin")]
        public async Task<IActionResult> GetMaintenanceHistory(int id)
        {
            try
            {
                var history = await _maintenanceHistoryDao.GetMaintenanceHistoryByIdAsync(id);
                if (history == null)
                    return NotFound(new ApiResponse<MaintenanceHistoryResponseDto>(404, "NotFound", "Maintenance history not found."));

                var dto = new MaintenanceHistoryResponseDto
                {
                    HistoryId = history.HistoryId,
                    VehicleId = history.VehicleId,
                    MaintenanceDate = history.MaintenanceDate,
                    Description = history.Description,
                    Notes = history.Notes,
                    Cost = history.Cost,
                    MileageAtMaintenance = history.MileageAtMaintenance,
                    CreatedAt = history.CreatedAt,
                    UpdatedAt = history.UpdatedAt
                };

                return Ok(new ApiResponse<MaintenanceHistoryResponseDto>(200, "Success", "Maintenance history retrieved successfully.", data: dto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> GetAllMaintenanceHistories([FromQuery] MaintenanceHistoryQueryParams queryParams)
        {
            try
            {
                // Center restriction: Staff chỉ xem maintenance histories tại center của họ
                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (userRole == UserRole.Staff.ToString())
                {
                    if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                        return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                    var (employee, error) = await GetCurrentEmployeeAsync(currentUserId, userRole);
                    if (error != null) return error;

                    // Force filter by staff's center - we need to filter by work orders at their center
                    // Get work order IDs at staff's center
                    var workOrderIdsAtCenter = await _context.WorkOrders
                        .Where(wo => wo.CenterId == employee!.CenterId)
                        .Select(wo => wo.WorkOrderId)
                        .ToListAsync();

                    var (allHistories, allTotal) = await _maintenanceHistoryDao.GetAllMaintenanceHistoriesAsync(queryParams);

                    // Filter histories by work orders at staff's center
                    var filteredHistories = allHistories
                        .Where(h => h.PartUsages.Any(pu => pu.Part?.CenterId == employee!.CenterId))
                        .ToList();

                    var staffDtos = filteredHistories.Select(h => new MaintenanceHistoryResponseDto
                    {
                        HistoryId = h.HistoryId,
                        VehicleId = h.VehicleId,
                        MaintenanceDate = h.MaintenanceDate,
                        Description = h.Description,
                        Notes = h.Notes,
                        Cost = h.Cost,
                        MileageAtMaintenance = h.MileageAtMaintenance,
                        CreatedAt = h.CreatedAt,
                        UpdatedAt = h.UpdatedAt
                    }).ToList();

                    var staffResponseData = new { histories = staffDtos, total = filteredHistories.Count, page = queryParams.Page, pageSize = queryParams.PageSize };
                    return Ok(new ApiResponse<object>(200, "Success", "Maintenance histories retrieved successfully.", data: staffResponseData));
                }

                var (histories, total) = await _maintenanceHistoryDao.GetAllMaintenanceHistoriesAsync(queryParams);

                var dtos = histories.Select(h => new MaintenanceHistoryResponseDto
                {
                    HistoryId = h.HistoryId,
                    VehicleId = h.VehicleId,
                    MaintenanceDate = h.MaintenanceDate,
                    Description = h.Description,
                    Notes = h.Notes,
                    Cost = h.Cost,
                    MileageAtMaintenance = h.MileageAtMaintenance,
                    CreatedAt = h.CreatedAt,
                    UpdatedAt = h.UpdatedAt
                }).ToList();

                var responseData = new { histories = dtos, total, page = queryParams.Page, pageSize = queryParams.PageSize };
                return Ok(new ApiResponse<object>(200, "Success", "Maintenance histories retrieved successfully.", data: responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("vehicle/{vehicleId}")]
        [Authorize(Roles = "Customer,Staff,Technician,Admin")]
        public async Task<IActionResult> GetMaintenanceHistoriesByVehicle(int vehicleId)
        {
            try
            {
                var histories = await _maintenanceHistoryDao.GetMaintenanceHistoriesByVehicleIdAsync(vehicleId);
                var dtos = histories.Select(h => new MaintenanceHistoryResponseDto
                {
                    HistoryId = h.HistoryId,
                    VehicleId = h.VehicleId,
                    MaintenanceDate = h.MaintenanceDate,
                    Description = h.Description,
                    Notes = h.Notes,
                    Cost = h.Cost,
                    MileageAtMaintenance = h.MileageAtMaintenance,
                    CreatedAt = h.CreatedAt,
                    UpdatedAt = h.UpdatedAt
                }).ToList();

                return Ok(new ApiResponse<List<MaintenanceHistoryResponseDto>>(200, "Success", "Maintenance histories retrieved successfully.", data: dtos));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Technician,Admin")]
        public async Task<IActionResult> UpdateMaintenanceHistory(int id, [FromBody] MaintenanceHistoryUpdateRequestDto dto)
        {
            try
            {
                if (id != dto.HistoryId)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Maintenance history ID mismatch."));

                var history = new MaintenanceHistory
                {
                    HistoryId = dto.HistoryId,
                    VehicleId = dto.VehicleId,
                    MaintenanceDate = dto.MaintenanceDate,
                    Description = dto.Description,
                    Notes = dto.Notes,
                    Cost = dto.Cost,
                    MileageAtMaintenance = dto.MileageAtMaintenance
                };

                var updatedHistory = await _maintenanceHistoryDao.UpdateMaintenanceHistoryAsync(history);
                var updatedDto = new MaintenanceHistoryResponseDto
                {
                    HistoryId = updatedHistory.HistoryId,
                    VehicleId = updatedHistory.VehicleId,
                    MaintenanceDate = updatedHistory.MaintenanceDate,
                    Description = updatedHistory.Description,
                    Notes = updatedHistory.Notes,
                    Cost = updatedHistory.Cost,
                    MileageAtMaintenance = updatedHistory.MileageAtMaintenance,
                    CreatedAt = updatedHistory.CreatedAt,
                    UpdatedAt = updatedHistory.UpdatedAt
                };

                return Ok(new ApiResponse<MaintenanceHistoryResponseDto>(200, "Success", "Maintenance history updated successfully.", data: updatedDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMaintenanceHistory(int id)
        {
            try
            {
                var success = await _maintenanceHistoryDao.DeleteMaintenanceHistoryAsync(id);
                if (!success)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Maintenance history not found."));

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
    }
}
