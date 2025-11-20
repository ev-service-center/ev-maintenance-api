using System.Security.Claims;
using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Helpers;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
using EVServiceCenterMaintenanceAPI.Services;
using EVServiceCenterMaintenanceAPI.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WorkOrderController : ControllerBase
    {
        private readonly WorkOrderDao _workOrderDao;
        private readonly EvserviceCenterDbContext _context;
        private readonly PayOSService _payOSService;
        private readonly ILogger<WorkOrderController> _logger;
        private readonly EmailService _emailService;
        private readonly EmployeeDao _employeeDao;

        public WorkOrderController(WorkOrderDao workOrderDao, EvserviceCenterDbContext context, PayOSService payOSService, ILogger<WorkOrderController> logger, EmailService emailService, EmployeeDao employeeDao)
        {
            _workOrderDao = workOrderDao;
            _context = context;
            _payOSService = payOSService;
            _logger = logger;
            _emailService = emailService;
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
        /// Validate Staff/Technician can only access work orders at their center
        /// </summary>
        private async Task<IActionResult?> ValidateCenterAccessAsync(int workOrderCenterId, string? userRole, int currentUserId)
        {
            if (userRole != UserRole.Staff.ToString() && userRole != UserRole.Technician.ToString())
                return null; // Not Staff/Technician, no restriction

            var currentEmployee = await _employeeDao.GetEmployeeByIdAsync(currentUserId);
            if (currentEmployee == null)
                return BadRequest(new ApiResponse<object>(400, "BadRequest",
                    $"{userRole} user does not have an associated employee record."));

            if (currentEmployee.Center == null || currentEmployee.Center.Status == ServiceCenterStatus.Deleted.ToString())
                return BadRequest(new ApiResponse<object>(400, "BadRequest", "Your service center has been deleted. Please contact administrator."));

            if (workOrderCenterId != currentEmployee.CenterId)
                return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                    $"{userRole} can only access work orders from their own service center (Center ID: {currentEmployee.CenterId})."));

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

            if (currentEmployee.Center == null || currentEmployee.Center.Status == ServiceCenterStatus.Deleted.ToString())
                return (null, BadRequest(new ApiResponse<object>(400, "BadRequest", "Your service center has been deleted. Please contact administrator.")));

            return (currentEmployee, null);
        }

        #endregion

        [HttpPost]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> CreateWorkOrder([FromBody] WorkOrderCreateRequestDto dto)
        {
            try
            {
                // Get current user info for authorization
                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                // Center restriction: Staff chỉ tạo work order tại center của họ
                if (userRole == UserRole.Staff.ToString())
                {
                    var centerAccessError = await ValidateCenterAccessAsync(dto.CenterId, userRole, currentUserId);
                    if (centerAccessError != null) return centerAccessError;
                }

                // Validation: Vehicle is not busy
                var isVehicleBusy = await _context.Vehicles
                    .Where(v => v.VehicleId == dto.VehicleId)
                    .AnyAsync(v =>
                        v.Appointments.Any(a =>
                            a.Status == AppointmentStatus.Pending.ToString() ||
                            a.Status == AppointmentStatus.Confirmed.ToString() ||
                            a.Status == AppointmentStatus.InProgress.ToString()) ||
                        v.WorkOrders.Any(w =>
                            w.Status == WorkOrderStatus.Pending.ToString() ||
                            w.Status == WorkOrderStatus.InProgress.ToString()));

                if (isVehicleBusy)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "This vehicle is currently busy with an active appointment or work order. Please complete or cancel the existing booking first."));

                var workOrder = new WorkOrder
                {
                    CenterId = dto.CenterId,
                    CustomerId = dto.CustomerId,
                    VehicleId = dto.VehicleId,
                    CreatedByStaffId = dto.CreatedByStaffId,
                    AppointmentId = dto.AppointmentId,
                    Status = dto.Status.ToString(),
                    CheckInAt = dto.CheckInAt,
                    CheckOutAt = dto.CheckOutAt,
                    OdometerKm = dto.OdometerKm,
                    Notes = dto.Notes
                };
                var createdWorkOrder = await _workOrderDao.CreateWorkOrderAsync(workOrder, dto.ServiceIds);
                var createdDto = new WorkOrderResponseDto
                {
                    WorkOrderId = createdWorkOrder.WorkOrderId,
                    CenterId = createdWorkOrder.CenterId,
                    CustomerId = createdWorkOrder.CustomerId,
                    VehicleId = createdWorkOrder.VehicleId,
                    CreatedByStaffId = createdWorkOrder.CreatedByStaffId,
                    AppointmentId = createdWorkOrder.AppointmentId,
                    Status = Enum.Parse<WorkOrderStatus>(createdWorkOrder.Status),
                    CheckInAt = createdWorkOrder.CheckInAt,
                    CheckOutAt = createdWorkOrder.CheckOutAt,
                    OdometerKm = createdWorkOrder.OdometerKm,
                    Notes = createdWorkOrder.Notes
                };
                return CreatedAtAction(nameof(GetWorkOrder), new { id = createdWorkOrder.WorkOrderId },
                    new ApiResponse<WorkOrderResponseDto>(201, "Created", "WorkOrder created successfully.", data: createdDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Staff,Technician,Admin")]
        public async Task<IActionResult> UpdateWorkOrder(int id, [FromBody] WorkOrderUpdateRequestDto dto)
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

                var existingWorkOrder = await _workOrderDao.GetWorkOrderByIdAsync(id);
                if (existingWorkOrder == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "WorkOrder not found."));

                // Center access for Staff/Technician
                if (userRole == UserRole.Staff.ToString() || userRole == UserRole.Technician.ToString())
                {
                    var centerAccessError = await ValidateCenterAccessAsync(existingWorkOrder.CenterId, userRole, currentUserId);
                    if (centerAccessError != null) return centerAccessError;
                }

                // Cannot update Completed or Cancelled work orders
                if (existingWorkOrder.Status == WorkOrderStatus.Completed.ToString() || existingWorkOrder.Status == WorkOrderStatus.Cancelled.ToString())
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        $"Cannot update {existingWorkOrder.Status} work order. Work order is finalized."));
                }

                // Technician: Can only update work orders with assigned services
                if (userRole == UserRole.Technician.ToString())
                {
                    var hasAssignedService = existingWorkOrder.AppointmentServices?
                        .Any(aps => aps.AssignedTechnicianId == currentUserId) ?? false;

                    if (!hasAssignedService)
                    {
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                            "You can only update work orders with services assigned to you."));
                    }
                }

                // Determine final values for cross-field validation
                var finalCustomerId = dto.CustomerId ?? existingWorkOrder.CustomerId;
                var finalVehicleId = dto.VehicleId ?? existingWorkOrder.VehicleId;
                var finalCheckInAt = dto.CheckInAt ?? existingWorkOrder.CheckInAt;
                var finalCheckOutAt = dto.CheckOutAt ?? existingWorkOrder.CheckOutAt;

                // Validate CheckIn <= CheckOut
                if (finalCheckInAt.HasValue && finalCheckOutAt.HasValue && finalCheckInAt.Value > finalCheckOutAt.Value)
                {
                    return BadRequest(new ApiResponse<object>(400, "ValidationError",
                        "CheckIn time cannot be after CheckOut time."));
                }

                // Validate Customer and Vehicle (if either changed)
                if (dto.CustomerId.HasValue || dto.VehicleId.HasValue)
                {
                    // Validate Customer (final)
                    var customer = await _context.Users
                        .Where(u => u.UserId == finalCustomerId && u.Status != UserStatus.Deleted.ToString())
                        .FirstOrDefaultAsync();
                    if (customer == null)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", $"Customer with ID {finalCustomerId} not found."));

                    if (customer.Status != UserStatus.Active.ToString())
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", "Customer account is not active."));

                    // Validate Vehicle (final)
                    var vehicle = await _context.Vehicles
                        .Where(v => v.VehicleId == finalVehicleId && v.Status != VehicleStatus.Inactive.ToString())
                        .FirstOrDefaultAsync();
                    if (vehicle == null)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", $"Vehicle with ID {finalVehicleId} not found."));

                    // Cross-field: Vehicle must belong to Customer
                    if (vehicle.CustomerId != finalCustomerId)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            $"Vehicle with ID {finalVehicleId} does not belong to customer with ID {finalCustomerId}."));
                }

                // Validate Center and Staff (if either changed)
                if (dto.CenterId.HasValue || dto.CreatedByStaffId.HasValue)
                {
                    var finalCenterId = dto.CenterId ?? existingWorkOrder.CenterId;
                    var finalCreatedByStaffId = dto.CreatedByStaffId ?? existingWorkOrder.CreatedByStaffId;

                    // Validate Center (final)
                    var center = await _context.ServiceCenters.FindAsync(finalCenterId);
                    if (center == null)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", $"Service center with ID {finalCenterId} not found."));

                    if (center.Status != ServiceCenterStatus.Open.ToString())
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", $"Service center '{center.CenterName}' is not open."));

                    // Validate Staff (final)
                    var staffToCheck = await _employeeDao.GetEmployeeByIdAsync(finalCreatedByStaffId);
                    if (staffToCheck == null)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", $"Staff with ID {finalCreatedByStaffId} not found."));

                    // Cross-field: Staff must belong to Center
                    if (staffToCheck.CenterId != finalCenterId)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            $"Staff with ID {finalCreatedByStaffId} does not belong to service center with ID {finalCenterId}."));
                }

                // Validate AppointmentId if provided (allow null for walk-in)
                if (dto.AppointmentId.HasValue)
                {
                    var appointment = await _context.Appointments.FindAsync(dto.AppointmentId.Value);
                    if (appointment == null)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", $"Appointment with ID {dto.AppointmentId.Value} not found."));
                }

                // Apply partial updates
                if (dto.CenterId.HasValue) existingWorkOrder.CenterId = dto.CenterId.Value;
                if (dto.CustomerId.HasValue) existingWorkOrder.CustomerId = dto.CustomerId.Value;
                if (dto.VehicleId.HasValue) existingWorkOrder.VehicleId = dto.VehicleId.Value;
                if (dto.CreatedByStaffId.HasValue) existingWorkOrder.CreatedByStaffId = dto.CreatedByStaffId.Value;
                if (dto.AppointmentId.HasValue) existingWorkOrder.AppointmentId = dto.AppointmentId.Value;
                if (dto.CheckInAt.HasValue) existingWorkOrder.CheckInAt = dto.CheckInAt.Value;
                if (dto.CheckOutAt.HasValue) existingWorkOrder.CheckOutAt = dto.CheckOutAt.Value;
                if (dto.OdometerKm.HasValue) existingWorkOrder.OdometerKm = dto.OdometerKm.Value;
                if (dto.Notes != null) existingWorkOrder.Notes = dto.Notes;

                var updatedWorkOrder = await _workOrderDao.UpdateWorkOrderAsync(existingWorkOrder);

                var responseDto = new WorkOrderResponseDto
                {
                    WorkOrderId = updatedWorkOrder.WorkOrderId,
                    CenterId = updatedWorkOrder.CenterId,
                    CustomerId = updatedWorkOrder.CustomerId,
                    VehicleId = updatedWorkOrder.VehicleId,
                    CreatedByStaffId = updatedWorkOrder.CreatedByStaffId,
                    AppointmentId = updatedWorkOrder.AppointmentId,
                    Status = Enum.Parse<WorkOrderStatus>(updatedWorkOrder.Status),
                    CheckInAt = updatedWorkOrder.CheckInAt,
                    CheckOutAt = updatedWorkOrder.CheckOutAt,
                    OdometerKm = updatedWorkOrder.OdometerKm,
                    Notes = updatedWorkOrder.Notes
                };

                return Ok(new ApiResponse<WorkOrderResponseDto>(200, "Success", "WorkOrder updated successfully.", data: responseDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpPut("{id}/status")]
        [Authorize(Roles = "Staff,Technician,Admin")]
        public async Task<IActionResult> UpdateWorkOrderStatus(int id, [FromBody] WorkOrderUpdateStatusRequestDto dto)
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

                if (!Enum.IsDefined(typeof(WorkOrderStatus), dto.Status))
                {
                    return BadRequest(new ApiResponse<object>(400, "ValidationError", "Invalid Status value."));
                }

                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                var existingWorkOrder = await _workOrderDao.GetWorkOrderByIdAsync(id);
                if (existingWorkOrder == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "WorkOrder not found."));

                //Center access for Staff/Technician
                if (userRole == UserRole.Staff.ToString() || userRole == UserRole.Technician.ToString())
                {
                    var centerAccessError = await ValidateCenterAccessAsync(existingWorkOrder.CenterId, userRole, currentUserId);
                    if (centerAccessError != null) return centerAccessError;
                }

                // Technician: Can only update status for work orders with assigned services
                if (userRole == UserRole.Technician.ToString())
                {
                    var hasAssignedService = existingWorkOrder.AppointmentServices?
                        .Any(aps => aps.AssignedTechnicianId == currentUserId) ?? false;

                    if (!hasAssignedService)
                    {
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                            "You can only update status for work orders with services assigned to you."));
                    }
                }

                // Update status
                existingWorkOrder.Status = dto.Status.ToString();

                var updatedWorkOrder = await _workOrderDao.UpdateWorkOrderAsync(existingWorkOrder);

                var responseDto = new WorkOrderResponseDto
                {
                    WorkOrderId = updatedWorkOrder.WorkOrderId,
                    CenterId = updatedWorkOrder.CenterId,
                    CustomerId = updatedWorkOrder.CustomerId,
                    VehicleId = updatedWorkOrder.VehicleId,
                    CreatedByStaffId = updatedWorkOrder.CreatedByStaffId,
                    AppointmentId = updatedWorkOrder.AppointmentId,
                    Status = Enum.Parse<WorkOrderStatus>(updatedWorkOrder.Status),
                    CheckInAt = updatedWorkOrder.CheckInAt,
                    CheckOutAt = updatedWorkOrder.CheckOutAt,
                    OdometerKm = updatedWorkOrder.OdometerKm,
                    Notes = updatedWorkOrder.Notes
                };

                return Ok(new ApiResponse<WorkOrderResponseDto>(200, "Success", "WorkOrder status updated successfully.", data: responseDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Customer,Staff,Technician,Admin")]
        public async Task<IActionResult> GetWorkOrder(int id)
        {
            try
            {
                int userId = JwtHelper.GetUserIdFromHttpContext(HttpContext);
                var userRole = User.FindFirstValue(ClaimTypes.Role);

                var workOrder = await _workOrderDao.GetWorkOrderByIdAsync(id);
                if (workOrder == null)
                    return NotFound(new ApiResponse<WorkOrderResponseDto>(404, "NotFound", "WorkOrder not found."));

                // Authorization check: Customer chỉ xem work order của mình
                if (userRole == UserRole.Customer.ToString() && workOrder.CustomerId != userId)
                {
                    return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                        "You can only view your own work orders."));
                }

                // Authorization check: Customer chỉ xem work order của mình
                if (userRole == UserRole.Customer.ToString() && workOrder.CustomerId != userId)
                {
                    return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                        "You can only view your own work orders."));
                }

                // Center restriction: Staff/Technician chỉ xem work order tại center của họ
                if (userRole == UserRole.Staff.ToString() || userRole == UserRole.Technician.ToString())
                {
                    var centerAccessError = await ValidateCenterAccessAsync(workOrder.CenterId, userRole, userId);
                    if (centerAccessError != null) return centerAccessError;
                }

                // Authorization check: Technician chỉ xem work order có service assigned cho mình
                if (userRole == UserRole.Technician.ToString())
                {
                    var hasAssignedService = workOrder.AppointmentServices?
                        .Any(aps => aps.AssignedTechnicianId == userId) ?? false;

                    if (!hasAssignedService)
                    {
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                            "You can only view work orders with services assigned to you."));
                    }
                }
                var dto = new WorkOrderResponseDto
                {
                    WorkOrderId = workOrder.WorkOrderId,
                    CenterId = workOrder.CenterId,
                    CustomerId = workOrder.CustomerId,
                    VehicleId = workOrder.VehicleId,
                    CreatedByStaffId = workOrder.CreatedByStaffId,
                    AppointmentId = workOrder.AppointmentId,
                    Status = Enum.Parse<WorkOrderStatus>(workOrder.Status),
                    CheckInAt = workOrder.CheckInAt,
                    CheckOutAt = workOrder.CheckOutAt,
                    OdometerKm = workOrder.OdometerKm,
                    Notes = workOrder.Notes,
                    CenterDetails = workOrder.Center == null ? null : new ServiceCenterResponseDto
                    {
                        CenterId = workOrder.Center.CenterId,
                        CenterName = workOrder.Center.CenterName,
                        Phone = workOrder.Center.Phone,
                        Email = workOrder.Center.Email,
                        Status = Enum.Parse<ServiceCenterStatus>(workOrder.Center.Status),
                        CreatedAt = workOrder.Center.CreatedAt,
                        UpdatedAt = workOrder.Center.UpdatedAt
                    },
                    CustomerDetails = workOrder.Customer == null ? null : new UserResponseDto
                    {
                        UserId = workOrder.Customer.UserId,
                        FullName = workOrder.Customer.FullName,
                        Email = workOrder.Customer.Email,
                        Phone = workOrder.Customer.Phone,
                        Role = Enum.Parse<UserRole>(workOrder.Customer.Role),
                        CreatedAt = workOrder.Customer.CreatedAt,
                        UpdatedAt = workOrder.Customer.UpdatedAt
                    },
                    VehicleDetails = workOrder.Vehicle == null ? null : new VehicleResponeDto
                    {
                        VehicleId = workOrder.Vehicle.VehicleId,
                        CustomerId = workOrder.Vehicle.CustomerId,
                        Model = workOrder.Vehicle.Model,
                        VIN = workOrder.Vehicle.Vin,
                        ManufactureYear = workOrder.Vehicle.ManufactureYear,
                        CurrentMileage = workOrder.Vehicle.CurrentMileage ?? 0,
                        LastMaintenanceDate = workOrder.Vehicle.LastMaintenanceDate,
                        Color = workOrder.Vehicle.Color,
                        Plate = workOrder.Vehicle.Plate,
                        CreatedAt = workOrder.Vehicle.CreatedAt,
                        UpdatedAt = workOrder.Vehicle.UpdatedAt
                    },
                    AppointmentServices = workOrder.AppointmentServices?.Select(aps => new AppointmentServiceResponseDto
                    {
                        AppointmentServiceId = aps.AppointmentServiceId,
                        WorkOrderId = aps.WorkOrderId,
                        ServiceId = aps.ServiceId,
                        Price = aps.Price
                    }).ToList(),
                    AppointmentDetails = workOrder.Appointment == null ? null : new AppointmentResponseDto
                    {
                        AppointmentId = workOrder.Appointment.AppointmentId,
                        CustomerId = workOrder.Appointment.CustomerId,
                        VehicleId = workOrder.Appointment.VehicleId,
                        CenterId = workOrder.Appointment.CenterId,
                        SlotId = workOrder.Appointment.SlotId,
                        AppointmentDate = workOrder.Appointment.AppointmentDate,
                        Status = Enum.Parse<AppointmentStatus>(workOrder.Appointment.Status),
                        Notes = workOrder.Appointment.Notes,
                        AssignedTechnicianId = workOrder.Appointment.AssignedTechnicianId,
                        Amount = workOrder.Appointment.Amount,
                        CreatedAt = workOrder.Appointment.CreatedAt,
                        UpdatedAt = workOrder.Appointment.UpdatedAt
                    },
                    ServiceDetails = workOrder.AppointmentServices?.Select(aps => new ServiceWithPartsResponseDto
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
                        UpdatedAt = aps.Service.UpdatedAt,
                        PartsUsed = workOrder.MaintenanceHistories?
                            .Where(mh => mh.ServiceId == aps.ServiceId)
                            .SelectMany(mh => mh.PartUsages ?? new List<PartUsage>())
                            .Select(pu => new PartUsageResponseDto
                            {
                                UsageId = pu.UsageId,
                                HistoryId = pu.HistoryId,
                                PartId = pu.PartId,
                                QuantityUsed = pu.QuantityUsed,
                                UnitCostPrice = pu.UnitCostPrice,
                                UnitPrice = pu.UnitPrice,
                                PartName = pu.Part?.PartName,
                                PartDescription = pu.Part?.Description,
                                TotalCost = pu.QuantityUsed * pu.UnitCostPrice,
                                TotalPrice = pu.QuantityUsed * pu.UnitPrice,
                                Profit = pu.QuantityUsed * (pu.UnitPrice - pu.UnitCostPrice),
                                ProfitMargin = pu.UnitPrice > 0 ? Math.Round((pu.UnitPrice - pu.UnitCostPrice) / pu.UnitPrice * 100, 2) : 0,
                                WorkOrderId = workOrder.WorkOrderId,
                                VehicleId = workOrder.VehicleId
                            }).ToList() ?? new List<PartUsageResponseDto>()
                    }).ToList()
                };
                return Ok(new ApiResponse<WorkOrderResponseDto>(200, "Success", "WorkOrder retrieved successfully.", data: dto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("{id}/payment-status")]
        [Authorize]
        public async Task<IActionResult> CheckPaymentStatus(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new ApiResponse<object>(400, "BadRequest", "Invalid work order ID."));
            }
            var workOrder = await _context.WorkOrders
                .Include(w => w.Appointment)
                    .ThenInclude(a => a.Slot)
                .Include(w => w.Invoice)
                .Include(w => w.Customer)
                .Include(w => w.Vehicle)
                .FirstOrDefaultAsync(w => w.WorkOrderId == id);
            if (workOrder == null)
            {
                return NotFound(new ApiResponse<object>(404, "NotFound", $"Work order with ID {id} not found."));
            }
            if (workOrder.Appointment == null)
            {
                return BadRequest(new ApiResponse<object>(400, "BadRequest", "No appointment found for this work order."));
            }
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "User not authenticated."));
            }
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin.ToString() || userRole == UserRole.Staff.ToString();
            var isOwner = currentUserId == workOrder.CustomerId;
            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new ApiResponse<object>(403, "Forbidden", "You are not authorized to check this work order's payment status."));
            }
            try
            {
                var serviceIds = await _context.AppointmentServices
                    .Where(aps => aps.WorkOrderId == workOrder.WorkOrderId)
                    .Select(aps => aps.ServiceId)
                    .ToListAsync();
                var services = await _context.Services
                    .Where(s => serviceIds.Contains(s.ServiceId))
                    .ToListAsync();
                decimal totalCost = services.Sum(s => s.BasePrice);

                if (string.IsNullOrEmpty(workOrder.OrderCode))
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "No payment link found for this work order. OrderCode is missing."));
                }

                long orderCode = long.Parse(workOrder.OrderCode);
                var paymentLinkInformation = await _payOSService.GetPaymentLinkInformation(orderCode);
                if (paymentLinkInformation.status == "PAID")
                {
                    if (paymentLinkInformation.amountPaid != totalCost || paymentLinkInformation.amountRemaining != 0)
                    {
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", "Payment amount mismatch."));
                    }

                    // Check if Payment record already exists (from webhook)
                    var existingPayment = await _context.Payments
                        .FirstOrDefaultAsync(p => p.WorkOrderId == workOrder.WorkOrderId);

                    if (existingPayment == null)
                    {

                        // Get transaction info from PayOS
                        var transaction = paymentLinkInformation.transactions?.FirstOrDefault();
                        if (transaction == null)
                        {
                            _logger.LogError("No transaction found in PaymentLinkInformation for WorkOrder {WorkOrderId}", workOrder.WorkOrderId);
                            return StatusCode(500, new ApiResponse<object>(500, "InternalServerError",
                                "Payment was successful but transaction details are missing. Please contact support."));
                        }

                        string paymentType;
                        int? invoiceId = null;

                        if (workOrder.Invoice == null)
                        {
                            paymentType = "Deposit";
                        }
                        else
                        {
                            paymentType = "Final";
                            invoiceId = workOrder.Invoice.InvoiceId;
                        }

                        // Parse payment date 
                        DateTime paymentDateTime = DateTime.Parse(transaction.transactionDateTime);

                        var payment = new Payment
                        {
                            WorkOrderId = workOrder.WorkOrderId,
                            InvoiceId = invoiceId,
                            Method = "PayOS",
                            Amount = paymentLinkInformation.amountPaid,
                            TransactionId = transaction.reference,
                            PaymentDate = paymentDateTime,
                            PaymentType = paymentType,
                            OrderCode = orderCode.ToString(),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.Payments.Add(payment);
                        _logger.LogInformation($"Payment record created manually for WorkOrder {workOrder.WorkOrderId} - Type: {paymentType}, Amount: {payment.Amount} (webhook may have failed)");

                        // Send confirmation email using FireAndForget
                        if (!string.IsNullOrEmpty(workOrder.Customer?.Email))
                        {
                            string vehicleInfo = workOrder.Vehicle != null
                                ? $"{workOrder.Vehicle.Model ?? "N/A"} ({workOrder.Vehicle.Plate ?? "N/A"})"
                                : "N/A";

                            string appointmentDate = workOrder.Appointment.Slot != null
                                ? workOrder.Appointment.Slot.StartTime.ConvertToVietnamTime().ToString("dd/MM/yyyy HH:mm")
                                : "N/A";

                            DateTime transactionTimeVN = DateTime.Parse(transaction.transactionDateTime);
                            string paymentDateFormatted = transactionTimeVN.ToString("dd/MM/yyyy HH:mm:ss");

                            string customerName = workOrder.Customer?.FullName ?? "Khách hàng";
                            string customerEmail = workOrder.Customer!.Email;
                            string transactionId = transaction.reference;
                            string orderCodeStr = orderCode.ToString();

                            if (paymentType == "Deposit")
                            {
                                decimal amountCopy = payment.Amount;
                                int workOrderIdCopy = workOrder.WorkOrderId;

                                TaskHelper.FireAndForget(async () =>
                                {
                                    try
                                    {
                                        await _emailService.SendDepositPaymentConfirmationEmailAsync(
                                            customerName,
                                            customerEmail,
                                            workOrderIdCopy,
                                            amountCopy,
                                            vehicleInfo,
                                            appointmentDate,
                                            paymentDateFormatted,
                                            transactionId,
                                            orderCodeStr
                                        );
                                        _logger.LogInformation($"Deposit payment confirmation email sent to {customerEmail} for WorkOrder {workOrderIdCopy}");
                                    }
                                    catch (Exception emailEx)
                                    {
                                        _logger.LogError(emailEx, $"Failed to send deposit payment confirmation email for WorkOrder {workOrderIdCopy}");
                                    }
                                });
                            }
                            else
                            {
                                int invoiceIdCopy = workOrder.Invoice?.InvoiceId ?? 0;
                                int workOrderIdCopy = workOrder.WorkOrderId;
                                decimal finalAmountCopy = payment.Amount;
                                decimal totalAmountCopy = workOrder.Invoice?.TotalAmount ?? 0;
                                string invoiceStatusCopy = workOrder.Invoice?.Status ?? "N/A";

                                TaskHelper.FireAndForget(async () =>
                                {
                                    try
                                    {
                                        await _emailService.SendFinalPaymentConfirmationEmailAsync(
                                            customerName,
                                            customerEmail,
                                            invoiceIdCopy,
                                            workOrderIdCopy,
                                            finalAmountCopy,
                                            totalAmountCopy,
                                            finalAmountCopy,
                                            vehicleInfo,
                                            paymentDateFormatted,
                                            transactionId,
                                            orderCodeStr,
                                            invoiceStatusCopy
                                        );
                                        _logger.LogInformation($"Final payment confirmation email sent to {customerEmail} for WorkOrder {workOrderIdCopy}");
                                    }
                                    catch (Exception emailEx)
                                    {
                                        _logger.LogError(emailEx, $"Failed to send final payment confirmation email for WorkOrder {workOrderIdCopy}");
                                    }
                                });
                            }
                        }
                    }

                    workOrder.Appointment.Status = AppointmentStatus.Confirmed.ToString();
                    await _context.SaveChangesAsync();
                    return Ok(new ApiResponse<object>(200, "Success", "Payment confirmed. Appointment status updated to Confirmed."));
                }
                else if (paymentLinkInformation.status == "CANCELLED")
                {
                    workOrder.Appointment.Status = AppointmentStatus.Cancelled.ToString();
                    var slot = await _context.AppointmentSlots.FindAsync(workOrder.Appointment.SlotId);
                    if (slot != null)
                    {
                        slot.IsAvailable = true;
                    }
                    await _context.SaveChangesAsync();
                    return Ok(new ApiResponse<object>(200, "CANCELLED", "Payment cancelled. Appointment status updated to Cancelled."));
                }
                else
                {
                    return Ok(new ApiResponse<object>(200, "Fail", $"Payment status: {paymentLinkInformation.status}"));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "InternalServerError", $"An error occurred while checking payment status: {ex.Message}"));
            }
        }

        [HttpGet]
        [Authorize(Roles = "Staff,Technician,Admin")]
        public async Task<IActionResult> GetAllWorkOrders([FromQuery] WorkOrderQueryParams queryParams)
        {
            try
            {
                // Center restriction: Staff/Technician chỉ xem work orders tại center của họ
                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (userRole == UserRole.Staff.ToString() || userRole == UserRole.Technician.ToString())
                {
                    if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                        return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                    var (employee, error) = await GetCurrentEmployeeAsync(currentUserId, userRole);
                    if (error != null) return error;

                    // Force filter by staff/technician's center
                    queryParams.CenterId = employee!.CenterId;
                }

                var (workOrders, total) = await _workOrderDao.GetAllWorkOrdersAsync(queryParams);

                // Additional filter for Technician: chỉ xem work orders có service assigned cho mình
                if (userRole == UserRole.Technician.ToString())
                {
                    if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                        return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                    workOrders = workOrders
                        .Where(wo => wo.AppointmentServices?
                            .Any(aps => aps.AssignedTechnicianId == currentUserId) ?? false)
                        .ToList();

                    // Cập nhật lại total sau khi filter
                    total = workOrders.Count;
                }
                var dtos = workOrders.Select(wo => new WorkOrderResponseDto
                {
                    WorkOrderId = wo.WorkOrderId,
                    CenterId = wo.CenterId,
                    CustomerId = wo.CustomerId,
                    VehicleId = wo.VehicleId,
                    CreatedByStaffId = wo.CreatedByStaffId,
                    AppointmentId = wo.AppointmentId,
                    Status = Enum.Parse<WorkOrderStatus>(wo.Status),
                    CheckInAt = wo.CheckInAt,
                    CheckOutAt = wo.CheckOutAt,
                    OdometerKm = wo.OdometerKm,
                    Notes = wo.Notes,
                    CenterDetails = wo.Center == null ? null : new ServiceCenterResponseDto
                    {
                        CenterId = wo.Center.CenterId,
                        CenterName = wo.Center.CenterName,
                        Phone = wo.Center.Phone,
                        Email = wo.Center.Email,
                        Status = Enum.Parse<ServiceCenterStatus>(wo.Center.Status),
                        CreatedAt = wo.Center.CreatedAt,
                        UpdatedAt = wo.Center.UpdatedAt
                    },
                    CustomerDetails = wo.Customer == null ? null : new UserResponseDto
                    {
                        UserId = wo.Customer.UserId,
                        FullName = wo.Customer.FullName,
                        Email = wo.Customer.Email,
                        Phone = wo.Customer.Phone,
                        Role = Enum.Parse<UserRole>(wo.Customer.Role),
                        CreatedAt = wo.Customer.CreatedAt,
                        UpdatedAt = wo.Customer.UpdatedAt
                    },
                    VehicleDetails = wo.Vehicle == null ? null : new VehicleResponeDto
                    {
                        VehicleId = wo.Vehicle.VehicleId,
                        CustomerId = wo.Vehicle.CustomerId,
                        Model = wo.Vehicle.Model,
                        VIN = wo.Vehicle.Vin,
                        ManufactureYear = wo.Vehicle.ManufactureYear,
                        CurrentMileage = wo.Vehicle.CurrentMileage ?? 0,
                        LastMaintenanceDate = wo.Vehicle.LastMaintenanceDate,
                        Color = wo.Vehicle.Color,
                        Plate = wo.Vehicle.Plate,
                        CreatedAt = wo.Vehicle.CreatedAt,
                        UpdatedAt = wo.Vehicle.UpdatedAt
                    },
                    AppointmentServices = wo.AppointmentServices?.Select(aps => new AppointmentServiceResponseDto
                    {
                        AppointmentServiceId = aps.AppointmentServiceId,
                        WorkOrderId = aps.WorkOrderId,
                        ServiceId = aps.ServiceId,
                        Price = aps.Price
                    }).ToList(),
                    AppointmentDetails = wo.Appointment == null ? null : new AppointmentResponseDto
                    {
                        AppointmentId = wo.Appointment.AppointmentId,
                        CustomerId = wo.Appointment.CustomerId,
                        VehicleId = wo.Appointment.VehicleId,
                        CenterId = wo.Appointment.CenterId,
                        SlotId = wo.Appointment.SlotId,
                        AppointmentDate = wo.Appointment.AppointmentDate,
                        Status = Enum.Parse<AppointmentStatus>(wo.Appointment.Status),
                        Notes = wo.Appointment.Notes,
                        AssignedTechnicianId = wo.Appointment.AssignedTechnicianId,
                        Amount = wo.Appointment.Amount,
                        CreatedAt = wo.Appointment.CreatedAt,
                        UpdatedAt = wo.Appointment.UpdatedAt
                    },
                    ServiceDetails = wo.AppointmentServices?.Select(aps => new ServiceWithPartsResponseDto
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
                        UpdatedAt = aps.Service.UpdatedAt,
                        PartsUsed = wo.MaintenanceHistories?
                            .Where(mh => mh.ServiceId == aps.ServiceId)
                            .SelectMany(mh => mh.PartUsages ?? new List<PartUsage>())
                            .Select(pu => new PartUsageResponseDto
                            {
                                UsageId = pu.UsageId,
                                HistoryId = pu.HistoryId,
                                PartId = pu.PartId,
                                QuantityUsed = pu.QuantityUsed,
                                UnitCostPrice = pu.UnitCostPrice,
                                UnitPrice = pu.UnitPrice,
                                PartName = pu.Part?.PartName,
                                PartDescription = pu.Part?.Description,
                                TotalCost = pu.QuantityUsed * pu.UnitCostPrice,
                                TotalPrice = pu.QuantityUsed * pu.UnitPrice,
                                Profit = pu.QuantityUsed * (pu.UnitPrice - pu.UnitCostPrice),
                                ProfitMargin = pu.UnitPrice > 0 ? Math.Round((pu.UnitPrice - pu.UnitCostPrice) / pu.UnitPrice * 100, 2) : 0,
                                WorkOrderId = wo.WorkOrderId,
                                VehicleId = wo.VehicleId
                            }).ToList() ?? new List<PartUsageResponseDto>()
                    }).ToList()
                }).ToList();

                var responseData = new { workOrders = dtos, total, page = queryParams.Page, pageSize = queryParams.PageSize };
                return Ok(new ApiResponse<object>(200, "Success", "WorkOrders retrieved successfully.", data: responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("customer/{customerId}")]
        [Authorize(Roles = "Customer,Staff,Admin")]
        public async Task<IActionResult> GetWorkOrdersByCustomer(int customerId)
        {
            try
            {
                // Get current user info
                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                // Authorization check: Customer chỉ xem work orders của mình
                if (userRole == UserRole.Customer.ToString() && customerId != currentUserId)
                {
                    return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                        "You can only view your own work orders."));
                }

                var workOrders = await _workOrderDao.GetWorkOrdersByCustomerIdAsync(customerId);

                // Center restriction: Staff chỉ xem work orders của customer tại center của họ
                if (userRole == UserRole.Staff.ToString())
                {
                    var (employee, error) = await GetCurrentEmployeeAsync(currentUserId, userRole);
                    if (error != null) return error;

                    // Filter work orders by staff's center
                    workOrders = workOrders.Where(wo => wo.CenterId == employee!.CenterId).ToList();
                }

                var dtos = workOrders.Select(wo => new WorkOrderResponseDto
                {
                    WorkOrderId = wo.WorkOrderId,
                    CenterId = wo.CenterId,
                    CustomerId = wo.CustomerId,
                    VehicleId = wo.VehicleId,
                    CreatedByStaffId = wo.CreatedByStaffId,
                    AppointmentId = wo.AppointmentId,
                    Status = Enum.Parse<WorkOrderStatus>(wo.Status),
                    CheckInAt = wo.CheckInAt,
                    CheckOutAt = wo.CheckOutAt,
                    OdometerKm = wo.OdometerKm,
                    Notes = wo.Notes,
                    CenterDetails = wo.Center == null ? null : new ServiceCenterResponseDto
                    {
                        CenterId = wo.Center.CenterId,
                        CenterName = wo.Center.CenterName,
                        Phone = wo.Center.Phone,
                        Email = wo.Center.Email,
                        Status = Enum.Parse<ServiceCenterStatus>(wo.Center.Status),
                        CreatedAt = wo.Center.CreatedAt,
                        UpdatedAt = wo.Center.UpdatedAt
                    },
                    CustomerDetails = wo.Customer == null ? null : new UserResponseDto
                    {
                        UserId = wo.Customer.UserId,
                        FullName = wo.Customer.FullName,
                        Email = wo.Customer.Email,
                        Phone = wo.Customer.Phone,
                        Role = Enum.Parse<UserRole>(wo.Customer.Role),
                        CreatedAt = wo.Customer.CreatedAt,
                        UpdatedAt = wo.Customer.UpdatedAt
                    },
                    VehicleDetails = wo.Vehicle == null ? null : new VehicleResponeDto
                    {
                        VehicleId = wo.Vehicle.VehicleId,
                        CustomerId = wo.Vehicle.CustomerId,
                        Model = wo.Vehicle.Model,
                        VIN = wo.Vehicle.Vin,
                        ManufactureYear = wo.Vehicle.ManufactureYear,
                        CurrentMileage = wo.Vehicle.CurrentMileage ?? 0,
                        LastMaintenanceDate = wo.Vehicle.LastMaintenanceDate,
                        Color = wo.Vehicle.Color,
                        Plate = wo.Vehicle.Plate,
                        CreatedAt = wo.Vehicle.CreatedAt,
                        UpdatedAt = wo.Vehicle.UpdatedAt
                    },
                    AppointmentServices = wo.AppointmentServices?.Select(aps => new AppointmentServiceResponseDto
                    {
                        AppointmentServiceId = aps.AppointmentServiceId,
                        WorkOrderId = aps.WorkOrderId,
                        ServiceId = aps.ServiceId,
                        Price = aps.Price
                    }).ToList(),
                    AppointmentDetails = wo.Appointment == null ? null : new AppointmentResponseDto
                    {
                        AppointmentId = wo.Appointment.AppointmentId,
                        CustomerId = wo.Appointment.CustomerId,
                        VehicleId = wo.Appointment.VehicleId,
                        CenterId = wo.Appointment.CenterId,
                        SlotId = wo.Appointment.SlotId,
                        AppointmentDate = wo.Appointment.AppointmentDate,
                        Status = Enum.Parse<AppointmentStatus>(wo.Appointment.Status),
                        Notes = wo.Appointment.Notes,
                        AssignedTechnicianId = wo.Appointment.AssignedTechnicianId,
                        Amount = wo.Appointment.Amount,
                        CreatedAt = wo.Appointment.CreatedAt,
                        UpdatedAt = wo.Appointment.UpdatedAt
                    },
                    ServiceDetails = wo.AppointmentServices?.Select(aps => new ServiceWithPartsResponseDto
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
                        UpdatedAt = aps.Service.UpdatedAt,
                        PartsUsed = wo.MaintenanceHistories?
                            .Where(mh => mh.ServiceId == aps.ServiceId)
                            .SelectMany(mh => mh.PartUsages ?? new List<PartUsage>())
                            .Select(pu => new PartUsageResponseDto
                            {
                                UsageId = pu.UsageId,
                                HistoryId = pu.HistoryId,
                                PartId = pu.PartId,
                                QuantityUsed = pu.QuantityUsed,
                                UnitCostPrice = pu.UnitCostPrice,
                                UnitPrice = pu.UnitPrice,
                                PartName = pu.Part?.PartName,
                                PartDescription = pu.Part?.Description,
                                TotalCost = pu.QuantityUsed * pu.UnitCostPrice,
                                TotalPrice = pu.QuantityUsed * pu.UnitPrice,
                                Profit = pu.QuantityUsed * (pu.UnitPrice - pu.UnitCostPrice),
                                ProfitMargin = pu.UnitPrice > 0 ? Math.Round((pu.UnitPrice - pu.UnitCostPrice) / pu.UnitPrice * 100, 2) : 0,
                                WorkOrderId = wo.WorkOrderId,
                                VehicleId = wo.VehicleId
                            }).ToList() ?? new List<PartUsageResponseDto>()
                    }).ToList()
                }).ToList();

                return Ok(new ApiResponse<List<WorkOrderResponseDto>>(200, "Success", "WorkOrders retrieved successfully.", data: dtos));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
    }
}
