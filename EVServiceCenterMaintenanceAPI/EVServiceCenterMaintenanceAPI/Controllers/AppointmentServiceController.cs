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
        private async Task<IActionResult?> ValidateCenterAccessAsync(int centerId, string? userRole, int currentUserId)
        {
            if (userRole != UserRole.Staff.ToString() && userRole != UserRole.Technician.ToString())
                return null; // Not Staff/Technician, no restriction

            var currentEmployee = await _employeeDao.GetEmployeeByIdAsync(currentUserId);
            if (currentEmployee == null)
                return BadRequest(new ApiResponse<object>(400, "BadRequest",
                    $"{userRole} user does not have an associated employee record."));

            if (currentEmployee.Center == null || currentEmployee.Center.Status == ServiceCenterStatus.Deleted.ToString())
                return BadRequest(new ApiResponse<object>(400, "BadRequest", "Your service center has been deleted. Please contact administrator."));

            if (centerId != currentEmployee.CenterId)
                return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                    $"{userRole} can only access appointment services from their own service center (Center ID: {currentEmployee.CenterId})."));

            return null;
        }

        private async Task<AppointmentServiceResponseDto> MapToResponseDtoAsync(AppointmentService aps, bool includeFullDetails = true)
        {
            var dto = new AppointmentServiceResponseDto
            {
                AppointmentServiceId = aps.AppointmentServiceId,
                WorkOrderId = aps.WorkOrderId,
                ServiceId = aps.ServiceId,
                Price = aps.Price,
                AssignedTechnicianId = aps.AssignedTechnicianId,
                Status = aps.Status,
                CreatedAt = aps.CreatedAt,
                UpdatedAt = aps.UpdatedAt,
                ServiceDetails = aps.Service == null ? null : new ServiceResponseDto
                {
                    ServiceId = aps.Service.ServiceId,
                    ServiceName = aps.Service.ServiceName,
                    Description = aps.Service.Description,
                    BasePrice = aps.Service.BasePrice,
                    EstimatedTime = aps.Service.EstimatedTime,
                    Status = Enum.Parse<ServiceStatus>(aps.Service.Status),
                    ReminderIntervalDays = aps.Service.ReminderIntervalDays ?? 0,
                    ReminderMileage = aps.Service.ReminderMileage ?? 0,
                    Notes = aps.Service.Notes,
                    CreatedAt = aps.Service.CreatedAt,
                    UpdatedAt = aps.Service.UpdatedAt
                },
                AssignedTechnicianDetails = aps.AssignedTechnicianId == null ? null : await _context.Users
                    .Where(u => u.UserId == aps.AssignedTechnicianId)
                    .Select(u => new UserResponseDto
                    {
                        UserId = u.UserId,
                        Username = u.Username,
                        FullName = u.FullName,
                        Email = u.Email,
                        Phone = u.Phone,
                        Role = Enum.Parse<UserRole>(u.Role),
                        Status = Enum.Parse<UserStatus>(u.Status),
                        Avatar = u.Avatar,
                        CreatedAt = u.CreatedAt,
                        UpdatedAt = u.UpdatedAt
                    })
                    .FirstOrDefaultAsync()
            };

            // Only include WorkOrderDetails if full details are requested
            if (includeFullDetails && aps.WorkOrder != null)
            {
                dto.WorkOrderDetails = new WorkOrderResponseDto
                {
                    WorkOrderId = aps.WorkOrder.WorkOrderId,
                    CenterId = aps.WorkOrder.CenterId,
                    CustomerId = aps.WorkOrder.CustomerId,
                    VehicleId = aps.WorkOrder.VehicleId,
                    CreatedByStaffId = aps.WorkOrder.CreatedByStaffId,
                    AppointmentId = aps.WorkOrder.AppointmentId,
                    Status = Enum.Parse<WorkOrderStatus>(aps.WorkOrder.Status),
                    CheckInAt = aps.WorkOrder.CheckInAt,
                    CheckOutAt = aps.WorkOrder.CheckOutAt,
                    OdometerKm = aps.WorkOrder.OdometerKm,
                    Notes = aps.WorkOrder.Notes,
                    OrderCode = aps.WorkOrder.OrderCode,
                    CenterDetails = aps.WorkOrder.Center == null ? null : new ServiceCenterResponseDto
                    {
                        CenterId = aps.WorkOrder.Center.CenterId,
                        CenterName = aps.WorkOrder.Center.CenterName,
                        Address = aps.WorkOrder.Center.Address,
                        Phone = aps.WorkOrder.Center.Phone,
                        Email = aps.WorkOrder.Center.Email,
                        Status = Enum.Parse<ServiceCenterStatus>(aps.WorkOrder.Center.Status),
                        CreatedAt = aps.WorkOrder.Center.CreatedAt,
                        UpdatedAt = aps.WorkOrder.Center.UpdatedAt
                    },
                    CustomerDetails = aps.WorkOrder.Customer == null ? null : new UserResponseDto
                    {
                        UserId = aps.WorkOrder.Customer.UserId,
                        Username = aps.WorkOrder.Customer.Username,
                        FullName = aps.WorkOrder.Customer.FullName,
                        Email = aps.WorkOrder.Customer.Email,
                        Phone = aps.WorkOrder.Customer.Phone,
                        Role = Enum.Parse<UserRole>(aps.WorkOrder.Customer.Role),
                        Status = Enum.Parse<UserStatus>(aps.WorkOrder.Customer.Status),
                        Avatar = aps.WorkOrder.Customer.Avatar,
                        CreatedAt = aps.WorkOrder.Customer.CreatedAt,
                        UpdatedAt = aps.WorkOrder.Customer.UpdatedAt
                    },
                    VehicleDetails = aps.WorkOrder.Vehicle == null ? null : new VehicleResponeDto
                    {
                        VehicleId = aps.WorkOrder.Vehicle.VehicleId,
                        CustomerId = aps.WorkOrder.Vehicle.CustomerId,
                        Model = aps.WorkOrder.Vehicle.Model,
                        VIN = aps.WorkOrder.Vehicle.Vin,
                        ManufactureYear = aps.WorkOrder.Vehicle.ManufactureYear,
                        CurrentMileage = aps.WorkOrder.Vehicle.CurrentMileage ?? 0,
                        LastMaintenanceDate = aps.WorkOrder.Vehicle.LastMaintenanceDate,
                        Color = aps.WorkOrder.Vehicle.Color,
                        Plate = aps.WorkOrder.Vehicle.Plate,
                        Status = Enum.Parse<VehicleStatus>(aps.WorkOrder.Vehicle.Status),
                        CreatedAt = aps.WorkOrder.Vehicle.CreatedAt,
                        UpdatedAt = aps.WorkOrder.Vehicle.UpdatedAt
                    },
                    CreatedByStaffDetails = await _context.Users
                        .Where(u => u.UserId == aps.WorkOrder.CreatedByStaffId)
                        .Select(u => new UserResponseDto
                        {
                            UserId = u.UserId,
                            Username = u.Username,
                            FullName = u.FullName,
                            Email = u.Email,
                            Phone = u.Phone,
                            Role = Enum.Parse<UserRole>(u.Role),
                            Status = Enum.Parse<UserStatus>(u.Status),
                            Avatar = u.Avatar,
                            CreatedAt = u.CreatedAt,
                            UpdatedAt = u.UpdatedAt
                        })
                        .FirstOrDefaultAsync(),
                    AppointmentServices = null,
                    ServiceDetails = null
                };
            }

            return dto;
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

                var updatedAppointmentService = await _appointmentServiceDao.UpdateStatusAsync(existingAppointmentService, dto.Status.ToString());

                // Only return basic info when updating status
                var responseDto = await MapToResponseDtoAsync(updatedAppointmentService, includeFullDetails: false);

                return Ok(new ApiResponse<AppointmentServiceResponseDto>(200, "Success", "AppointmentService status updated successfully.", data: responseDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Staff,Technician,Admin")]
        public async Task<IActionResult> UpdateAppointmentService(int id, [FromBody] AppointmentServiceUpdateRequestDto dto)
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

                // Cannot update if WorkOrder is Completed or Cancelled
                if (existingAppointmentService.WorkOrder.Status == WorkOrderStatus.Completed.ToString() ||
                    existingAppointmentService.WorkOrder.Status == WorkOrderStatus.Cancelled.ToString())
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        $"Cannot update AppointmentService. WorkOrder is {existingAppointmentService.WorkOrder.Status}."));
                }

                // Technician restrictions
                if (userRole == UserRole.Technician.ToString())
                {
                    // Can only update services assigned to them
                    if (existingAppointmentService.AssignedTechnicianId != currentUserId)
                    {
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                            "You can only update services assigned to you."));
                    }

                    // Technician cannot change AssignedTechnicianId
                    if (dto.AssignedTechnicianId.HasValue && dto.AssignedTechnicianId.Value != currentUserId)
                    {
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                            "Technicians cannot reassign services to other technicians."));
                    }
                }

                // Validate Price if provided
                if (dto.Price.HasValue && dto.Price.Value < 0)
                {
                    return BadRequest(new ApiResponse<object>(400, "ValidationError", "Price cannot be negative."));
                }

                // Validate ServiceId if provided
                if (dto.ServiceId.HasValue)
                {
                    var service = await _context.Services
                        .Where(s => s.ServiceId == dto.ServiceId.Value && s.Status != ServiceStatus.Inactive.ToString())
                        .FirstOrDefaultAsync();
                    if (service == null)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", $"Service with ID {dto.ServiceId.Value} not found."));
                }

                // Validate AssignedTechnicianId if provided (only Staff/Admin can change this)
                if (dto.AssignedTechnicianId.HasValue)
                {
                    if (userRole == UserRole.Technician.ToString())
                    {
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                            "Technicians cannot assign or reassign services."));
                    }

                    var technician = await _context.Users
                        .Where(u => u.UserId == dto.AssignedTechnicianId.Value && u.Status != UserStatus.Deleted.ToString())
                        .FirstOrDefaultAsync();
                    if (technician == null)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", $"User with ID {dto.AssignedTechnicianId.Value} not found."));

                    if (technician.Role != UserRole.Technician.ToString())
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", $"User with ID {dto.AssignedTechnicianId.Value} is not a Technician."));

                    // Validate Technician is an Employee
                    var technicianEmployee = await _employeeDao.GetEmployeeByIdAsync(dto.AssignedTechnicianId.Value);
                    if (technicianEmployee == null)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", $"Technician with ID {dto.AssignedTechnicianId.Value} does not have an associated employee record."));

                    // Cross-field: Technician must belong to same Center as WorkOrder
                    if (technicianEmployee.CenterId != existingAppointmentService.WorkOrder.CenterId)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            $"Technician with ID {dto.AssignedTechnicianId.Value} does not belong to the same service center as the WorkOrder (Center ID: {existingAppointmentService.WorkOrder.CenterId})."));
                }

                // Apply partial updates
                if (dto.ServiceId.HasValue) existingAppointmentService.ServiceId = dto.ServiceId.Value;
                if (dto.Price.HasValue) existingAppointmentService.Price = dto.Price.Value;
                if (dto.AssignedTechnicianId.HasValue) existingAppointmentService.AssignedTechnicianId = dto.AssignedTechnicianId.Value;
                if (dto.Status.HasValue) existingAppointmentService.Status = dto.Status.Value.ToString();

                var updatedAppointmentService = await _appointmentServiceDao.UpdateAppointmentServiceAsync(existingAppointmentService);

                var responseDto = await MapToResponseDtoAsync(updatedAppointmentService);

                return Ok(new ApiResponse<AppointmentServiceResponseDto>(200, "Success", "AppointmentService updated successfully.", data: responseDto));
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
                var technician = await _context.Users
                    .Where(u => u.UserId == dto.TechnicianId && u.Status != UserStatus.Deleted.ToString())
                    .FirstOrDefaultAsync();
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

                var updatedAppointmentService = await _appointmentServiceDao.AssignTechnicianAsync(existingAppointmentService, dto.TechnicianId);

                // Only return basic info when assigning technician
                var responseDto = await MapToResponseDtoAsync(updatedAppointmentService, includeFullDetails: false);

                return Ok(new ApiResponse<AppointmentServiceResponseDto>(200, "Success", "Technician assigned successfully.", data: responseDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpPut("batch-assign-technician")]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> BatchAssignTechnician([FromBody] AppointmentServiceBatchAssignTechnicianRequestDto dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Empty request (DTO null)."));
                }

                if (!ModelState.IsValid)
                {
                    var validationErrors = ModelState
                        .Where(kvp => !string.IsNullOrEmpty(kvp.Key) && kvp.Value?.Errors?.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? []);
                    return BadRequest(new ApiResponse<object>(400, "Validation Error", "One or more validation errors occurred.", validationErrors));
                }

                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                if (dto.Assignments == null || dto.Assignments.Count == 0)
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "At least one assignment is required."));
                }

                Employee? currentStaffEmployee = null;
                if (userRole == UserRole.Staff.ToString())
                {
                    currentStaffEmployee = await _employeeDao.GetEmployeeByIdAsync(currentUserId);
                    if (currentStaffEmployee == null)
                    {
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", "Staff user does not have an associated employee record."));
                    }
                }

                // Get all unique appointment service IDs and technician IDs
                var allAppointmentServiceIds = dto.Assignments.SelectMany(a => a.AppointmentServiceIds).Distinct().ToList();
                var allTechnicianIds = dto.Assignments.Select(a => a.TechnicianId).Distinct().ToList();

                // Get all appointment services
                var appointmentServices = await _appointmentServiceDao.GetAppointmentServicesByIdsAsync(allAppointmentServiceIds);
                var appointmentServiceDict = appointmentServices.ToDictionary(aps => aps.AppointmentServiceId);

                // Validate all appointment services exist
                var missingServiceIds = allAppointmentServiceIds.Except(appointmentServiceDict.Keys).ToList();
                if (missingServiceIds.Any())
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        $"Some AppointmentServices not found: {string.Join(", ", missingServiceIds)}"));
                }

                // Get all technicians
                var technicians = await _context.Users
                    .Where(u => allTechnicianIds.Contains(u.UserId))
                    .ToListAsync();
                var technicianDict = technicians.ToDictionary(t => t.UserId);

                // Get all technician employees
                var technicianEmployees = new Dictionary<int, Employee>();
                foreach (var techId in allTechnicianIds)
                {
                    var employee = await _employeeDao.GetEmployeeByIdAsync(techId);
                    if (employee != null)
                    {
                        technicianEmployees[techId] = employee;
                    }
                }

                // Validate each assignment and collect errors
                var errors = new List<BatchAssignError>();
                var validAssignments = new Dictionary<int, int>(); // appointmentServiceId -> technicianId

                foreach (var assignment in dto.Assignments)
                {
                    // Validate Technician exists
                    if (!technicianDict.TryGetValue(assignment.TechnicianId, out var technician))
                    {
                        // Add error for all services in this assignment
                        foreach (var appointmentServiceId in assignment.AppointmentServiceIds)
                        {
                            errors.Add(new BatchAssignError
                            {
                                AppointmentServiceId = appointmentServiceId,
                                ErrorMessage = $"User with ID {assignment.TechnicianId} not found."
                            });
                        }
                        continue;
                    }

                    // Validate Technician role
                    if (technician.Role != UserRole.Technician.ToString())
                    {
                        foreach (var appointmentServiceId in assignment.AppointmentServiceIds)
                        {
                            errors.Add(new BatchAssignError
                            {
                                AppointmentServiceId = appointmentServiceId,
                                ErrorMessage = $"User with ID {assignment.TechnicianId} is not a Technician."
                            });
                        }
                        continue;
                    }

                    // Validate Technician is an Employee
                    if (!technicianEmployees.TryGetValue(assignment.TechnicianId, out var technicianEmployee))
                    {
                        foreach (var appointmentServiceId in assignment.AppointmentServiceIds)
                        {
                            errors.Add(new BatchAssignError
                            {
                                AppointmentServiceId = appointmentServiceId,
                                ErrorMessage = $"Technician with ID {assignment.TechnicianId} does not have an associated employee record."
                            });
                        }
                        continue;
                    }

                    // Validate each appointment service in this assignment
                    foreach (var appointmentServiceId in assignment.AppointmentServiceIds)
                    {
                        if (!appointmentServiceDict.TryGetValue(appointmentServiceId, out var appointmentService))
                        {
                            errors.Add(new BatchAssignError
                            {
                                AppointmentServiceId = appointmentServiceId,
                                ErrorMessage = "AppointmentService not found."
                            });
                            continue;
                        }

                        // Validate AppointmentService has WorkOrder
                        if (appointmentService.WorkOrder == null)
                        {
                            errors.Add(new BatchAssignError
                            {
                                AppointmentServiceId = appointmentServiceId,
                                ErrorMessage = "AppointmentService does not have an associated WorkOrder."
                            });
                            continue;
                        }

                        // Validate Technician belongs to same Center as WorkOrder
                        if (technicianEmployee.CenterId != appointmentService.WorkOrder.CenterId)
                        {
                            errors.Add(new BatchAssignError
                            {
                                AppointmentServiceId = appointmentServiceId,
                                ErrorMessage = $"Technician with ID {assignment.TechnicianId} does not belong to the same service center as the WorkOrder (Center ID: {appointmentService.WorkOrder.CenterId})."
                            });
                            continue;
                        }

                        // Validate WorkOrder status
                        if (appointmentService.WorkOrder.Status == WorkOrderStatus.Completed.ToString() ||
                            appointmentService.WorkOrder.Status == WorkOrderStatus.Cancelled.ToString())
                        {
                            errors.Add(new BatchAssignError
                            {
                                AppointmentServiceId = appointmentServiceId,
                                ErrorMessage = $"Cannot assign technician. WorkOrder is {appointmentService.WorkOrder.Status}."
                            });
                            continue;
                        }

                        // Center access for Staff
                        if (userRole == UserRole.Staff.ToString())
                        {
                            if (appointmentService.WorkOrder.CenterId != currentStaffEmployee!.CenterId)
                            {
                                errors.Add(new BatchAssignError
                                {
                                    AppointmentServiceId = appointmentServiceId,
                                    ErrorMessage = "Staff can only access appointment services from their own service center."
                                });
                                continue;
                            }
                        }

                        // All validations passed for this service
                        validAssignments[appointmentServiceId] = assignment.TechnicianId;
                    }
                }

                // If all assignments are invalid, return error
                if (validAssignments.Count == 0)
                {
                    var responseDto = new AppointmentServiceBatchAssignTechnicianResponseDto
                    {
                        TotalRequested = allAppointmentServiceIds.Count,
                        SuccessCount = 0,
                        FailedCount = errors.Count,
                        SuccessItems = new List<AppointmentServiceResponseDto>(),
                        Errors = errors
                    };
                    return BadRequest(new ApiResponse<AppointmentServiceBatchAssignTechnicianResponseDto>(400, "BadRequest",
                        "All assignments failed validation.", data: responseDto));
                }

                // Perform batch assign for valid assignments
                var updatedAppointmentServices = await _appointmentServiceDao.BatchAssignTechnicianAsync(validAssignments);

                var successItems = new List<AppointmentServiceResponseDto>();
                foreach (var aps in updatedAppointmentServices)
                {
                    successItems.Add(await MapToResponseDtoAsync(aps, includeFullDetails: false));
                }

                var successResponseDto = new AppointmentServiceBatchAssignTechnicianResponseDto
                {
                    TotalRequested = allAppointmentServiceIds.Count,
                    SuccessCount = updatedAppointmentServices.Count,
                    FailedCount = errors.Count,
                    SuccessItems = successItems,
                    Errors = errors
                };

                var message = errors.Count > 0
                    ? $"Successfully assigned technicians to {successResponseDto.SuccessCount} appointment service(s). {errors.Count} assignment(s) failed."
                    : $"Successfully assigned technicians to {successResponseDto.SuccessCount} appointment service(s).";

                return Ok(new ApiResponse<AppointmentServiceBatchAssignTechnicianResponseDto>(200, "Success", message, data: successResponseDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
    }
}