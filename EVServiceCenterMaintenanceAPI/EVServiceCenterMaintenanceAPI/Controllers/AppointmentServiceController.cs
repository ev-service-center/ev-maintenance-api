using System.Security.Claims;
using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Helpers;
using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentServiceController : ControllerBase
    {
        private readonly AppointmentServiceDao _appointmentServiceDao;
        private readonly EmployeeDao _employeeDao;
        private readonly EvserviceCenterDbContext _context;

        public AppointmentServiceController(
            AppointmentServiceDao appointmentServiceDao,
            EmployeeDao employeeDao,
            EvserviceCenterDbContext context)
        {
            _appointmentServiceDao = appointmentServiceDao;
            _employeeDao = employeeDao;
            _context = context;
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
        /// Validate Staff/Technician can only access appointment services at their center
        /// </summary>
        private async Task<IActionResult?> ValidateCenterAccessAsync(int workOrderCenterId, string? userRole, int currentUserId)
        {
            if (userRole != UserRole.Staff.ToString() && userRole != UserRole.Technician.ToString())
                return null; // Not Staff/Technician, no restriction

            var currentEmployee = await _employeeDao.GetEmployeeByIdAsync(currentUserId);
            if (currentEmployee == null)
                return BadRequest(new ApiResponse<object>(400, "BadRequest",
                    $"{userRole} user does not have an associated employee record."));

            if (workOrderCenterId != currentEmployee.CenterId)
                return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                    $"{userRole} can only access appointment services from their own service center (Center ID: {currentEmployee.CenterId})."));

            return null;
        }

        #endregion

        [HttpPut("{id}/status")]
        [Authorize(Roles = "Staff,Technician,Admin")]
        public async Task<IActionResult> UpdateAppointmentServiceStatus(int id, [FromBody] AppointmentServiceUpdateStatusRequestDto dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Empty request (DTO null)."));
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(kvp => !string.IsNullOrEmpty(kvp.Key) && kvp.Key != "id" && kvp.Value?.Errors?.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? []);
                    return BadRequest(new ApiResponse<object>(400, "Validation Error", "One or more validation errors occurred.", errors));
                }

                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                var existingAppointmentService = await _appointmentServiceDao.GetAppointmentServiceByIdAsync(id);
                if (existingAppointmentService == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "AppointmentService not found."));

                // Validate WorkOrder exists
                if (existingAppointmentService.WorkOrder == null)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "AppointmentService does not have an associated WorkOrder."));

                // Center access for Staff/Technician
                if (userRole == UserRole.Staff.ToString() || userRole == UserRole.Technician.ToString())
                {
                    var centerAccessError = await ValidateCenterAccessAsync(existingAppointmentService.WorkOrder.CenterId, userRole, currentUserId);
                    if (centerAccessError != null) return centerAccessError;
                }

                // Technician: Can only update services assigned to them
                if (userRole == UserRole.Technician.ToString())
                {
                    if (existingAppointmentService.AssignedTechnicianId != currentUserId)
                    {
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                            "You can only update status for services assigned to you."));
                    }
                }

                // Cannot update if WorkOrder is Completed or Cancelled
                if (existingAppointmentService.WorkOrder.Status == WorkOrderStatus.Completed.ToString() ||
                    existingAppointmentService.WorkOrder.Status == WorkOrderStatus.Cancelled.ToString())
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        $"Cannot update AppointmentService. WorkOrder is {existingAppointmentService.WorkOrder.Status}."));
                }

                // Update status
                existingAppointmentService.Status = dto.Status;

                var updatedAppointmentService = await _appointmentServiceDao.UpdateAppointmentServiceAsync(existingAppointmentService);

                var responseDto = new AppointmentServiceResponseDto
                {
                    AppointmentServiceId = updatedAppointmentService.AppointmentServiceId,
                    WorkOrderId = updatedAppointmentService.WorkOrderId,
                    ServiceId = updatedAppointmentService.ServiceId,
                    Price = updatedAppointmentService.Price,
                    AssignedTechnicianId = updatedAppointmentService.AssignedTechnicianId,
                    Status = updatedAppointmentService.Status,
                    CreatedAt = updatedAppointmentService.CreatedAt,
                    UpdatedAt = updatedAppointmentService.UpdatedAt
                };

                return Ok(new ApiResponse<AppointmentServiceResponseDto>(200, "Success", "AppointmentService status updated successfully.", data: responseDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpPut("{id}/assign-technician")]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> UpdateAppointmentServiceAssignTechnician(int id, [FromBody] AppointmentServiceAssignTechnicianRequestDto dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Empty request (DTO null)."));
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(kvp => !string.IsNullOrEmpty(kvp.Key) && kvp.Key != "id" && kvp.Value?.Errors?.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? []);
                    return BadRequest(new ApiResponse<object>(400, "Validation Error", "One or more validation errors occurred.", errors));
                }

                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                var existingAppointmentService = await _appointmentServiceDao.GetAppointmentServiceByIdAsync(id);
                if (existingAppointmentService == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "AppointmentService not found."));

                // Validate WorkOrder exists
                if (existingAppointmentService.WorkOrder == null)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "AppointmentService does not have an associated WorkOrder."));

                // Center access for Staff
                if (userRole == UserRole.Staff.ToString())
                {
                    var centerAccessError = await ValidateCenterAccessAsync(existingAppointmentService.WorkOrder.CenterId, userRole, currentUserId);
                    if (centerAccessError != null) return centerAccessError;
                }

                // Cannot update if WorkOrder is Completed or Cancelled
                if (existingAppointmentService.WorkOrder.Status == WorkOrderStatus.Completed.ToString() ||
                    existingAppointmentService.WorkOrder.Status == WorkOrderStatus.Cancelled.ToString())
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        $"Cannot assign technician. WorkOrder is {existingAppointmentService.WorkOrder.Status}."));
                }

                // Validate Technician exists and is a Technician role
                var technician = await _context.Users.FindAsync(dto.TechnicianId);
                if (technician == null)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", $"User with ID {dto.TechnicianId} not found."));

                if (technician.Role != UserRole.Technician.ToString())
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", $"User with ID {dto.TechnicianId} is not a Technician."));

                // Validate Technician is an Employee
                var technicianEmployee = await _employeeDao.GetEmployeeByIdAsync(dto.TechnicianId);
                if (technicianEmployee == null)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", $"Technician with ID {dto.TechnicianId} does not have an associated employee record."));

                // Cross-field: Technician must belong to same Center as WorkOrder
                if (technicianEmployee.CenterId != existingAppointmentService.WorkOrder.CenterId)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        $"Technician with ID {dto.TechnicianId} does not belong to the same service center as the WorkOrder (Center ID: {existingAppointmentService.WorkOrder.CenterId})."));

                // Update assigned technician
                existingAppointmentService.AssignedTechnicianId = dto.TechnicianId;

                var updatedAppointmentService = await _appointmentServiceDao.UpdateAppointmentServiceAsync(existingAppointmentService);

                var responseDto = new AppointmentServiceResponseDto
                {
                    AppointmentServiceId = updatedAppointmentService.AppointmentServiceId,
                    WorkOrderId = updatedAppointmentService.WorkOrderId,
                    ServiceId = updatedAppointmentService.ServiceId,
                    Price = updatedAppointmentService.Price,
                    AssignedTechnicianId = updatedAppointmentService.AssignedTechnicianId,
                    Status = updatedAppointmentService.Status,
                    CreatedAt = updatedAppointmentService.CreatedAt,
                    UpdatedAt = updatedAppointmentService.UpdatedAt
                };

                return Ok(new ApiResponse<AppointmentServiceResponseDto>(200, "Success", "Technician assigned successfully.", data: responseDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
    }
}