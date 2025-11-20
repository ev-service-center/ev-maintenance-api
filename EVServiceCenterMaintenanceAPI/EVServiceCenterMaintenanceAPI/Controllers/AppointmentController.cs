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
using Net.payOS.Types;
using System.Security.Claims;
using static EVServiceCenterMaintenanceAPI.Enums.HostBookingUrl;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentController : ControllerBase
    {
        private readonly AppointmentDao _appointmentDao;
        private readonly WorkOrderDao _workOrderDao;
        private readonly EmailService _emailService;
        private readonly EvserviceCenterDbContext _context;
        private readonly PayOSService _payOSService;
        private readonly UserDao _userDao;
        private readonly EmployeeDao _employeeDao;
        public AppointmentController(AppointmentDao appointmentDao, WorkOrderDao workOrderDao, EmailService email, EvserviceCenterDbContext context, PayOSService payOSService, UserDao userDao, EmployeeDao employeeDao)
        {
            _appointmentDao = appointmentDao;
            _workOrderDao = workOrderDao;
            _emailService = email;
            _context = context;
            _payOSService = payOSService;
            _userDao = userDao;
            _employeeDao = employeeDao;
        }

        #region Helper Methods

        /// <summary>
        /// Get current authenticated user from JWT token
        /// </summary>
        private async Task<(bool Success, User? User, IActionResult? ErrorResponse)> GetCurrentUserAsync()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                return (false, null, Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID.")));

            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null)
                return (false, null, NotFound(new ApiResponse<object>(404, "NotFound", "User not found.")));

            return (true, currentUser, null);
        }

        /// <summary>
        /// Validate customer can only access their own resources
        /// </summary>
        private IActionResult? ValidateCustomerAccess(User user, int targetCustomerId, string action = "access")
        {
            if (user.Role == UserRole.Customer.ToString() && targetCustomerId != user.UserId)
            {
                return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                    $"You can only {action} your own resources."));
            }
            return null;
        }

        /// <summary>
        /// Validate service center exists
        /// </summary>
        private async Task<IActionResult?> ValidateCenterExistsAsync(int centerId)
        {
            var exists = await _context.ServiceCenters.AnyAsync(sc => sc.CenterId == centerId);
            if (!exists)
                return NotFound(new ApiResponse<object>(404, "NotFound", $"Service Center with ID {centerId} not found."));
            return null;
        }

        /// <summary>
        /// Validate vehicle belongs to customer
        /// </summary>
        private async Task<IActionResult?> ValidateVehicleBelongsToCustomerAsync(int vehicleId, int customerId)
        {
            var vehicle = await _context.Vehicles
                .Where(v => v.VehicleId == vehicleId && v.Status != VehicleStatus.Inactive.ToString())
                .FirstOrDefaultAsync();
            if (vehicle == null)
                return NotFound(new ApiResponse<object>(404, "NotFound", $"Vehicle with ID {vehicleId} not found."));

            if (vehicle.CustomerId != customerId)
                return BadRequest(new ApiResponse<object>(400, "BadRequest",
                    "The selected vehicle does not belong to the specified customer."));

            return null;
        }

        /// <summary>
        /// Validate and lock appointment slot with pessimistic locking
        /// </summary>
        private async Task<(bool Success, AppointmentSlot? Slot, IActionResult? ErrorResponse)> ValidateAndLockSlotAsync(int slotId, int centerId)
        {
            var slot = await _context.AppointmentSlots
                .FromSqlRaw("SELECT * FROM AppointmentSlots WITH (UPDLOCK, ROWLOCK) WHERE SlotId = {0}", slotId)
                .FirstOrDefaultAsync();

            if (slot == null || !slot.IsAvailable)
            {
                return (false, null, BadRequest(new ApiResponse<object>(400, "BadRequest",
                    "The selected slot is not available.")));
            }

            if (slot.CenterId != centerId)
            {
                return (false, null, BadRequest(new ApiResponse<object>(400, "BadRequest",
                    "The selected slot does not belong to the specified service center.")));
            }

            return (true, slot, null);
        }

        /// <summary>
        /// Validate services exist and calculate total cost
        /// </summary>
        private async Task<(bool Success, decimal TotalCost, IActionResult? ErrorResponse)> ValidateServicesAndCalculateCostAsync(List<int> serviceIds)
        {
            if (serviceIds.Count == 0)
                return (true, 0, null);

            var services = await _context.Services
                .Where(s => serviceIds.Contains(s.ServiceId))
                .ToListAsync();

            if (services.Count != serviceIds.Count)
            {
                return (false, 0, BadRequest(new ApiResponse<object>(400, "BadRequest",
                    "One or more service IDs are invalid.")));
            }

            var totalCost = services.Sum(s => s.BasePrice);
            return (true, totalCost, null);
        }

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
        /// Validate Staff/Technician can only access appointments at their center
        /// </summary>
        private async Task<IActionResult?> ValidateCenterAccessAsync(int appointmentCenterId, string? userRole, int currentUserId)
        {
            if (userRole != UserRole.Staff.ToString() && userRole != UserRole.Technician.ToString())
                return null; // Not Staff/Technician, no restriction

            var currentEmployee = await _employeeDao.GetEmployeeByIdAsync(currentUserId);
            if (currentEmployee == null)
                return BadRequest(new ApiResponse<object>(400, "BadRequest",
                    $"{userRole} user does not have an associated employee record."));

            if (currentEmployee.Center == null || currentEmployee.Center.Status == ServiceCenterStatus.Deleted.ToString())
                return BadRequest(new ApiResponse<object>(400, "BadRequest", "Your service center has been deleted. Please contact administrator."));

            if (appointmentCenterId != currentEmployee.CenterId)
                return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                    $"{userRole} can only access appointments from their own service center (Center ID: {currentEmployee.CenterId})."));

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

        /// <summary>
        /// Map Appointment entity to AppointmentResponseDto
        /// </summary>
        private static AppointmentResponseDto MapToResponseDto(Appointment appointment, bool includeDetails = false)
        {
            var dto = new AppointmentResponseDto
            {
                AppointmentId = appointment.AppointmentId,
                CustomerId = appointment.CustomerId,
                VehicleId = appointment.VehicleId,
                CenterId = appointment.CenterId,
                SlotId = appointment.SlotId,
                AppointmentDate = appointment.AppointmentDate,
                Status = Enum.Parse<AppointmentStatus>(appointment.Status ?? AppointmentStatus.Pending.ToString()),
                Notes = appointment.Notes,
                AssignedTechnicianId = appointment.AssignedTechnicianId,
                Amount = appointment.Amount,
                CreatedAt = appointment.CreatedAt,
                UpdatedAt = appointment.UpdatedAt
            };

            if (includeDetails)
            {
                dto.VehicleDetails = appointment.Vehicle == null ? null : new VehicleResponeDto
                {
                    VehicleId = appointment.Vehicle.VehicleId,
                    CustomerId = appointment.Vehicle.CustomerId,
                    Model = appointment.Vehicle.Model,
                    VIN = appointment.Vehicle.Vin,
                    ManufactureYear = appointment.Vehicle.ManufactureYear,
                    CurrentMileage = appointment.Vehicle.CurrentMileage ?? 0,
                    LastMaintenanceDate = appointment.Vehicle.LastMaintenanceDate,
                    Color = appointment.Vehicle.Color,
                    Plate = appointment.Vehicle.Plate,
                    CreatedAt = appointment.Vehicle.CreatedAt,
                    UpdatedAt = appointment.Vehicle.UpdatedAt
                };

                dto.CustomerDetails = appointment.Customer == null ? null : new UserResponseDto
                {
                    UserId = appointment.Customer.UserId,
                    FullName = appointment.Customer.FullName,
                    Email = appointment.Customer.Email,
                    Phone = appointment.Customer.Phone,
                    Role = Enum.Parse<UserRole>(appointment.Customer.Role ?? UserRole.Customer.ToString()),
                    CreatedAt = appointment.Customer.CreatedAt,
                    UpdatedAt = appointment.Customer.UpdatedAt
                };

                dto.CenterDetails = appointment.Center == null ? null : new ServiceCenterResponseDto
                {
                    CenterId = appointment.Center.CenterId,
                    CenterName = appointment.Center.CenterName,
                    Phone = appointment.Center.Phone,
                    Email = appointment.Center.Email,
                    Status = Enum.Parse<ServiceCenterStatus>(appointment.Center.Status ?? ServiceCenterStatus.Open.ToString()),
                    CreatedAt = appointment.Center.CreatedAt,
                    UpdatedAt = appointment.Center.UpdatedAt
                };

                dto.SlotDetails = appointment.Slot == null ? null : new AppointmentSlotResponseDto
                {
                    SlotId = appointment.Slot.SlotId,
                    CenterId = appointment.Slot.CenterId,
                    StartTime = appointment.Slot.StartTime,
                    EndTime = appointment.Slot.EndTime,
                    CreatedAt = appointment.Slot.CreatedAt,
                    UpdatedAt = appointment.Slot.UpdatedAt
                };
            }

            return dto;
        }

        #endregion
        [HttpPost]
        [Authorize(Roles = "Customer,Staff,Admin")]
        public async Task<IActionResult> CreateAppointment([FromBody] AppointmentCreateRequestDto dto)
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

                // Get current user
                var (success, currentUser, errorResponse) = await GetCurrentUserAsync();
                if (!success) return errorResponse!;

                // Authorization check: Customer chỉ tạo appointment cho mình
                var accessError = ValidateCustomerAccess(currentUser!, dto.CustomerId, "create appointments for");
                if (accessError != null) return accessError;

                // Center restriction: Staff/Technician chỉ tạo appointment cho center của họ
                var (_, userRole) = GetCurrentUserInfo();
                if (userRole == UserRole.Staff.ToString() || userRole == UserRole.Technician.ToString())
                {
                    var centerAccessError = await ValidateCenterAccessAsync(dto.CenterId, userRole, currentUser!.UserId);
                    if (centerAccessError != null) return centerAccessError;
                }

                // Validation: Center exists
                var centerError = await ValidateCenterExistsAsync(dto.CenterId);
                if (centerError != null) return centerError;

                // Validation: Vehicle belongs to customer
                var vehicleError = await ValidateVehicleBelongsToCustomerAsync(dto.VehicleId, dto.CustomerId);
                if (vehicleError != null) return vehicleError;

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

                // Validation: Service Center is Open
                var center = await _context.ServiceCenters.FindAsync(dto.CenterId);
                if (center!.Status != ServiceCenterStatus.Open.ToString())
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "Service center is not open. Cannot create appointment."));

                // Validation: Customer is Active
                var customer = await _userDao.GetUserByIdAsync(dto.CustomerId);
                if (customer == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Customer not found."));
                if (customer.Status != UserStatus.Active.ToString())
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "Customer account is not active. Cannot create appointment."));

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Validate and lock slot
                    var (slotSuccess, slot, slotError) = await ValidateAndLockSlotAsync(dto.SlotId, dto.CenterId);
                    if (!slotSuccess)
                    {
                        await transaction.RollbackAsync();
                        return slotError!;
                    }

                    // Validate slot time has not passed
                    var nowVietnam = DateTime.UtcNow.ConvertToVietnamTime();
                    var slotStartTimeVN = slot!.StartTime.ConvertToVietnamTime();
                    if (slotStartTimeVN <= nowVietnam)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            "Cannot create appointment for a slot that has already passed. Please select a future slot."));
                    }

                    // Validate services and calculate cost
                    var serviceIds = dto.ServiceIds ?? [];
                    var (servicesSuccess, totalCost, servicesError) = await ValidateServicesAndCalculateCostAsync(serviceIds);
                    if (!servicesSuccess)
                    {
                        await transaction.RollbackAsync();
                        return servicesError!;
                    }

                    // Create appointment - AppointmentDate is auto-set from Slot.StartTime
                    var appointment = new Appointment
                    {
                        CustomerId = dto.CustomerId,
                        VehicleId = dto.VehicleId,
                        CenterId = dto.CenterId,
                        SlotId = dto.SlotId,
                        AppointmentDate = slot.StartTime,
                        Notes = dto.Notes,
                        Status = AppointmentStatus.Pending.ToString(),
                        Amount = totalCost,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    var createdAppointment = await _appointmentDao.CreateAppointmentAsync(appointment);

                    // Mark slot as unavailable (Staff creates confirmed appointments without payment)
                    slot.IsAvailable = false;

                    // Create work order
                    var workOrder = new WorkOrder
                    {
                        CenterId = dto.CenterId,
                        CustomerId = dto.CustomerId,
                        VehicleId = dto.VehicleId,
                        CreatedByStaffId = currentUser!.UserId,
                        AppointmentId = createdAppointment.AppointmentId,
                        Status = WorkOrderStatus.Pending.ToString(),
                        CheckInAt = null,
                        CheckOutAt = null,
                        OdometerKm = null,
                        Notes = dto.Notes
                    };
                    var createdWorkOrder = await _workOrderDao.CreateWorkOrderAsync(workOrder, serviceIds);

                    await transaction.CommitAsync();

                    // Reload appointment with includes for detailed response
                    var appointmentWithDetails = await _appointmentDao.GetAppointmentByIdAsync(createdAppointment.AppointmentId);

                    // Map to response DTO
                    var createdDto = MapToResponseDto(appointmentWithDetails!, includeDetails: true);
                    createdDto.WorkOrderDetails = new WorkOrderResponseDto
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

                    return CreatedAtAction(nameof(GetAppointment), new { id = createdAppointment.AppointmentId },
                        new ApiResponse<AppointmentResponseDto>(201, "Created", "Appointment and WorkOrder created successfully.", data: createdDto));
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
        [HttpPost("booking")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> BookingAppointment([FromBody] AppointmentCreateRequestDto dto)
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

                // Get current user
                var (success, currentUser, errorResponse) = await GetCurrentUserAsync();
                if (!success) return errorResponse!;

                // Authorization: Customer chỉ book cho mình (force customerId = userId)
                dto.CustomerId = currentUser!.UserId;

                // Validation: Center exists
                var centerError = await ValidateCenterExistsAsync(dto.CenterId);
                if (centerError != null) return centerError;

                // Validation: Vehicle belongs to customer
                var vehicleError = await ValidateVehicleBelongsToCustomerAsync(dto.VehicleId, dto.CustomerId);
                if (vehicleError != null) return vehicleError;

                // Validation: Vehicle is not busy (no active appointment/workorder)
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

                // Validation: Service Center is Open
                var center = await _context.ServiceCenters.FindAsync(dto.CenterId);
                if (center!.Status != ServiceCenterStatus.Open.ToString())
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "Service center is not open. Cannot create appointment."));

                // Validation: Customer is Active
                if (currentUser!.Status != UserStatus.Active.ToString())
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "Your account is not active. Cannot create appointment."));

                // Validation: BookingAppointment phải có ít nhất 1 service
                var serviceIds = dto.ServiceIds ?? [];
                if (serviceIds.Count == 0)
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "At least one service must be selected for booking."));
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Validate and lock slot
                    var (slotSuccess, slot, slotError) = await ValidateAndLockSlotAsync(dto.SlotId, dto.CenterId);
                    if (!slotSuccess)
                    {
                        await transaction.RollbackAsync();
                        return slotError!;
                    }

                    // Validate slot time has not passed
                    var nowVietnam = DateTime.UtcNow.ConvertToVietnamTime();
                    var slotStartTimeVN = slot!.StartTime.ConvertToVietnamTime();
                    if (slotStartTimeVN <= nowVietnam)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            "Cannot book a slot that has already passed. Please select a future slot."));
                    }

                    // Validate services and calculate cost
                    var (servicesSuccess, totalCost, servicesError) = await ValidateServicesAndCalculateCostAsync(serviceIds);
                    if (!servicesSuccess)
                    {
                        await transaction.RollbackAsync();
                        return servicesError!;
                    }


                    // Create appointment - AppointmentDate is auto-set from Slot.StartTime
                    var appointment = new Appointment
                    {
                        CustomerId = dto.CustomerId,
                        VehicleId = dto.VehicleId,
                        CenterId = dto.CenterId,
                        SlotId = dto.SlotId,
                        AppointmentDate = slot.StartTime,
                        Notes = dto.Notes,
                        Status = AppointmentStatus.Pending.ToString(),
                        Amount = totalCost,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    var createdAppointment = await _appointmentDao.CreateAppointmentAsync(appointment);

                    // Create work order
                    var workOrder = new WorkOrder
                    {
                        CenterId = dto.CenterId,
                        CustomerId = dto.CustomerId,
                        VehicleId = dto.VehicleId,
                        CreatedByStaffId = currentUser.UserId,
                        AppointmentId = createdAppointment.AppointmentId,
                        Status = WorkOrderStatus.Pending.ToString(),
                        CheckInAt = null,
                        CheckOutAt = null,
                        OdometerKm = null,
                        Notes = dto.Notes
                    };
                    var createdWorkOrder = await _workOrderDao.CreateWorkOrderAsync(workOrder, serviceIds);

                    // Create payment link
                    var services = await _context.Services
                        .Where(s => serviceIds.Contains(s.ServiceId))
                        .ToListAsync();
                    string cancelUrl = GetCancelUrl(HostEnvironment.Local, createdWorkOrder.WorkOrderId);
                    string successUrl = GetSuccessUrl(HostEnvironment.Local, createdWorkOrder.WorkOrderId);
                    var items = services.Select(s => new ItemData(
                         s.ServiceName,
                         1,
                         (int)s.BasePrice
                     )).ToList();
                    // Create payment link with 15-minute expiration (default)
                    // Customer must pay within 15 minutes to reserve the slot
                    var paymentResult = await _payOSService.CreatePaymentLink(
                        createdWorkOrder.WorkOrderId,
                        totalCost,
                        "Payment for EV",
                        items,
                        cancelUrl,
                        successUrl
                    // expirationMinutes: 15 (default - slot reservation time)
                    );

                    createdWorkOrder.OrderCode = paymentResult.orderCode.ToString();
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    // Reload appointment with includes for detailed response
                    var appointmentWithDetails = await _appointmentDao.GetAppointmentByIdAsync(createdAppointment.AppointmentId);

                    // Map to response DTO
                    var createdDto = MapToResponseDto(appointmentWithDetails!, includeDetails: true);
                    createdDto.PaymentLink = paymentResult.checkoutUrl;
                    createdDto.WorkOrderDetails = new WorkOrderResponseDto
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

                    return CreatedAtAction(nameof(GetAppointment), new { id = createdAppointment.AppointmentId },
                        new ApiResponse<AppointmentResponseDto>(201, "Created", "Appointment and WorkOrder created successfully.", data: createdDto));
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
        [HttpGet("{id}")]
        [Authorize(Roles = "Customer,Staff,Technician,Admin")]
        public async Task<IActionResult> GetAppointment(int id)
        {
            try
            {
                var appointment = await _appointmentDao.GetAppointmentByIdAsync(id);
                if (appointment == null)
                    return NotFound(new ApiResponse<AppointmentResponseDto>(404, "NotFound", "Appointment not found."));

                // Authorization checks
                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                // Customer chỉ xem appointment của mình
                if (userRole == UserRole.Customer.ToString() && appointment.CustomerId != currentUserId)
                {
                    return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                        "You can only view your own appointments."));
                }

                // Staff/Technician chỉ xem appointment tại center của họ
                if (userRole == UserRole.Staff.ToString() || userRole == UserRole.Technician.ToString())
                {
                    var centerError = await ValidateCenterAccessAsync(appointment.CenterId, userRole, currentUserId);
                    if (centerError != null) return centerError;
                }

                var dto = MapToResponseDto(appointment, includeDetails: true);
                return Ok(new ApiResponse<AppointmentResponseDto>(200, "Success", "Appointment retrieved successfully.", data: dto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
        [HttpPut("maintain/info/status/{id}")]
        [Authorize(Roles = "Staff,Technician,Admin")]
        public async Task<IActionResult> UpdateAppointmentStatus(int id, [FromBody] AppointmentUpdateStatusRequestDto dtoUpdate)
        {
            try
            {
                // Get appointment để check center access
                var existingAppointment = await _appointmentDao.GetAppointmentByIdAsync(id);
                if (existingAppointment == null)
                    return NotFound(new ApiResponse<AppointmentResponseDto>(404, "NotFound", "Appointment not found."));

                // Center restriction: Staff/Technician chỉ update appointment tại center của họ
                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                if (userRole == UserRole.Staff.ToString() || userRole == UserRole.Technician.ToString())
                {
                    var centerError = await ValidateCenterAccessAsync(existingAppointment.CenterId, userRole, currentUserId);
                    if (centerError != null) return centerError;
                }

                // Validate status transition
                var currentStatus = Enum.Parse<AppointmentStatus>(existingAppointment.Status);
                var newStatus = dtoUpdate.Status;

                // Cannot change status from Completed or Cancelled
                if (currentStatus == AppointmentStatus.Completed)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "Cannot change status of completed appointment. Appointment is already finalized."));

                if (currentStatus == AppointmentStatus.Cancelled)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "Cannot change status of cancelled appointment. Create a new appointment instead."));

                // Validate valid transitions
                bool isValidTransition = (currentStatus, newStatus) switch
                {
                    // From Pending
                    (AppointmentStatus.Pending, AppointmentStatus.Confirmed) => true,  // Payment received
                    (AppointmentStatus.Pending, AppointmentStatus.Cancelled) => true,

                    // From Confirmed
                    (AppointmentStatus.Confirmed, AppointmentStatus.InProgress) => true,  // Check-in
                    (AppointmentStatus.Confirmed, AppointmentStatus.Cancelled) => true,

                    // From InProgress
                    (AppointmentStatus.InProgress, AppointmentStatus.Completed) => true,  // Check-out
                    (AppointmentStatus.InProgress, AppointmentStatus.Cancelled) => true,  // Rare case

                    // Same status (no change)
                    _ when currentStatus == newStatus => true,

                    // All other transitions invalid
                    _ => false
                };

                if (!isValidTransition)
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        $"Invalid status transition from {currentStatus} to {newStatus}. " +
                        $"Valid transitions: Pending→Confirmed/Cancelled, Confirmed→InProgress/Cancelled, InProgress→Completed/Cancelled."));
                }

                // Update status (DAO returns updated appointment with includes)
                var updatedAppointment = await _appointmentDao.UpdateAppointmentStatusAsync(id, dtoUpdate.Status);
                if (updatedAppointment == null)
                    return NotFound(new ApiResponse<AppointmentResponseDto>(404, "NotFound", "Appointment not found."));

                // Map to response DTO
                var dto = MapToResponseDto(updatedAppointment, includeDetails: true);

                // Send email notification
                TaskHelper.FireAndForget(
                    dto,
                    updatedAppointment.Customer?.Email,
                    _emailService.SendChangeInfoAppointmentEmailAsync
                );

                return Ok(new ApiResponse<AppointmentResponseDto>(200, "Success", "Appointment status updated successfully.", data: dto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
        [HttpGet]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> GetAllAppointments([FromQuery] AppointmentQueryParams queryParams)
        {
            try
            {
                // Center restriction: Staff chỉ xem appointments tại center của họ
                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (userRole == UserRole.Staff.ToString())
                {
                    if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                        return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                    var (employee, error) = await GetCurrentEmployeeAsync(currentUserId, userRole);
                    if (error != null) return error;

                    // Force filter by staff's center
                    queryParams.CenterId = employee!.CenterId;
                }

                var (appointments, total) = await _appointmentDao.GetAllAppointmentsAsync(queryParams);
                var dtos = appointments.Select(a => MapToResponseDto(a, includeDetails: true)).ToList();
                var responseData = new { appointments = dtos, total, page = queryParams.Page, pageSize = queryParams.PageSize };
                return Ok(new ApiResponse<object>(200, "Success", "Appointments retrieved successfully.", data: responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
        [HttpGet("customer/{customerId}")]
        [Authorize(Roles = "Customer,Staff,Admin")]
        public async Task<IActionResult> GetAppointmentsByCustomer(int customerId)
        {
            try
            {
                // Get current user
                var (success, currentUser, errorResponse) = await GetCurrentUserAsync();
                if (!success) return errorResponse!;

                // Authorization check: Customer chỉ xem appointments của mình
                var accessError = ValidateCustomerAccess(currentUser!, customerId, "view appointments for");
                if (accessError != null) return accessError;

                var appointments = await _appointmentDao.GetAppointmentsByCustomerIdAsync(customerId);

                // Center restriction: Staff chỉ xem appointments của customer tại center của họ
                if (currentUser!.Role == UserRole.Staff.ToString())
                {
                    var (employee, error) = await GetCurrentEmployeeAsync(currentUser.UserId, currentUser.Role);
                    if (error != null) return error;

                    // Filter appointments by staff's center
                    appointments = appointments.Where(a => a.CenterId == employee!.CenterId).ToList();
                }
                var dtos = appointments.Select(a => MapToResponseDto(a, includeDetails: true)).ToList();
                return Ok(new ApiResponse<List<AppointmentResponseDto>>(200, "Success", "Appointments retrieved successfully.", data: dtos));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
        [HttpGet("technician/{technicianId}")]
        [Authorize(Roles = "Technician,Staff,Admin")]
        public async Task<IActionResult> GetAppointmentsByTechnician(int technicianId)
        {
            try
            {
                // Get current user
                var (success, currentUser, errorResponse) = await GetCurrentUserAsync();
                if (!success) return errorResponse!;

                // Authorization check: Technician chỉ xem appointments của mình
                if (currentUser!.Role == UserRole.Technician.ToString() && technicianId != currentUser.UserId)
                {
                    return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                        "You can only view your own assigned appointments."));
                }

                var appointments = await _appointmentDao.GetAppointmentsByTechnicianIdAsync(technicianId);

                // Center restriction: Staff chỉ xem appointments của technician tại center của họ
                if (currentUser.Role == UserRole.Staff.ToString())
                {
                    var (employee, error) = await GetCurrentEmployeeAsync(currentUser.UserId, currentUser.Role);
                    if (error != null) return error;

                    // Verify technician belongs to staff's center
                    var technicianEmployee = await _employeeDao.GetEmployeeByIdAsync(technicianId);
                    if (technicianEmployee == null || technicianEmployee.CenterId != employee!.CenterId)
                    {
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                            $"You can only view appointments of technicians from your own service center (Center ID: {employee!.CenterId})."));
                    }

                    // Additional filter: appointments must also be at staff's center
                    appointments = appointments.Where(a => a.CenterId == employee.CenterId).ToList();
                }
                var dtos = appointments.Select(a => MapToResponseDto(a, includeDetails: true)).ToList();
                return Ok(new ApiResponse<List<AppointmentResponseDto>>(200, "Success", "Appointments retrieved successfully.", data: dtos));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
        [HttpPut("{id}")]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> UpdateAppointment(int id, [FromBody] AppointmentUpdateRequestDto dto)
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

                var existingAppointment = await _context.Appointments.FindAsync(id);
                if (existingAppointment == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Appointment not found."));

                // Business rule: Cannot update Completed or Cancelled appointments
                if (existingAppointment.Status == AppointmentStatus.Completed.ToString())
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "Cannot update completed appointment. Appointment is finalized."));

                if (existingAppointment.Status == AppointmentStatus.Cancelled.ToString())
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "Cannot update cancelled appointment. Please create a new appointment."));

                // Center restriction: Staff chỉ update appointment tại center của họ
                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (userRole == UserRole.Staff.ToString())
                {
                    if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                        return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                    var centerAccessError = await ValidateCenterAccessAsync(existingAppointment.CenterId, userRole, currentUserId);
                    if (centerAccessError != null) return centerAccessError;

                    // Staff cũng chỉ update sang center của họ (nếu có thay đổi center)
                    if (dto.CenterId.HasValue)
                    {
                        var newCenterError = await ValidateCenterAccessAsync(dto.CenterId.Value, userRole, currentUserId);
                        if (newCenterError != null) return newCenterError;
                    }
                }

                // Determine final values for validation
                var finalCustomerId = dto.CustomerId ?? existingAppointment.CustomerId;
                var finalVehicleId = dto.VehicleId ?? existingAppointment.VehicleId;
                var finalCenterId = dto.CenterId ?? existingAppointment.CenterId;

                // Validation: Center exists and is Open (nếu thay đổi)
                if (dto.CenterId.HasValue)
                {
                    var centerError = await ValidateCenterExistsAsync(dto.CenterId.Value);
                    if (centerError != null) return centerError;

                    var center = await _context.ServiceCenters.FindAsync(dto.CenterId.Value);
                    if (center!.Status != ServiceCenterStatus.Open.ToString())
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            "Service center is not open. Cannot update appointment to this center."));
                }

                // Validation: Customer is Active (nếu thay đổi)
                if (dto.CustomerId.HasValue)
                {
                    var customer = await _userDao.GetUserByIdAsync(dto.CustomerId.Value);
                    if (customer == null)
                        return NotFound(new ApiResponse<object>(404, "NotFound", "Customer not found."));
                    if (customer.Status != UserStatus.Active.ToString())
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            "Customer account is not active. Cannot update appointment."));
                }

                // Validation: Vehicle belongs to customer (nếu có thay đổi)
                if (dto.CustomerId.HasValue || dto.VehicleId.HasValue)
                {
                    var vehicleError = await ValidateVehicleBelongsToCustomerAsync(finalVehicleId, finalCustomerId);
                    if (vehicleError != null) return vehicleError;
                }

                // Validation: AppointmentDate không được trong quá khứ (theo giờ Vietnam) (nếu thay đổi)
                if (dto.AppointmentDate.HasValue)
                {
                    var todayVietnam = TimeZoneHelper.TodayInVietnam;
                    var appointmentDateVN = dto.AppointmentDate.Value.ConvertToVietnamTime();
                    if (appointmentDateVN.Date < todayVietnam)
                    {
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            "Appointment date cannot be in the past."));
                    }
                }

                // Business rule: Status changes should use dedicated endpoints
                if (dto.Status.HasValue)
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "Status cannot be updated through this endpoint. Use PUT /api/Appointment/maintain/info/status/{id} to update status or PUT /api/Appointment/{id}/cancel to cancel."));
                }

                // Validation: SlotId exists, belongs to center, and handle slot availability (nếu thay đổi)
                if (dto.SlotId.HasValue)
                {
                    var newSlot = await _context.AppointmentSlots.FindAsync(dto.SlotId.Value);
                    if (newSlot == null)
                        return NotFound(new ApiResponse<object>(404, "NotFound", $"Appointment slot with ID {dto.SlotId.Value} not found."));

                    if (newSlot.CenterId != finalCenterId)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            "The selected slot does not belong to the specified service center."));

                    // Validate slot time has not passed
                    var nowVietnam = DateTime.UtcNow.ConvertToVietnamTime();
                    var slotStartTimeVN = newSlot.StartTime.ConvertToVietnamTime();
                    if (slotStartTimeVN <= nowVietnam)
                    {
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            "Cannot update to a slot that has already passed. Please select a future slot."));
                    }

                    // Check new slot is available
                    if (!newSlot.IsAvailable)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            "The selected slot is not available."));

                    // Free old slot if changing
                    if (existingAppointment.SlotId != dto.SlotId.Value)
                    {
                        var oldSlot = await _context.AppointmentSlots.FindAsync(existingAppointment.SlotId);
                        if (oldSlot != null)
                        {
                            oldSlot.IsAvailable = true;
                        }
                        // Mark new slot as unavailable
                        newSlot.IsAvailable = false;
                    }
                }

                // Validation: AssignedTechnicianId exists and is a Technician at the center (nếu thay đổi)
                if (dto.AssignedTechnicianId.HasValue)
                {
                    var technician = await _userDao.GetUserByIdAsync(dto.AssignedTechnicianId.Value);
                    if (technician == null)
                        return NotFound(new ApiResponse<object>(404, "NotFound", $"User with ID {dto.AssignedTechnicianId.Value} not found."));

                    if (technician.Role != UserRole.Technician.ToString())
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            "Assigned user must have Technician role."));

                    // Validation: Technician is Active
                    if (technician.Status != UserStatus.Active.ToString())
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            "Technician account is not active. Cannot assign."));

                    var technicianEmployee = await _employeeDao.GetEmployeeByIdAsync(dto.AssignedTechnicianId.Value);
                    if (technicianEmployee == null)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            "Technician does not have an associated employee record."));

                    if (technicianEmployee.CenterId != finalCenterId)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            "Technician does not belong to the appointment's service center."));
                }

                // Apply partial updates (with transaction if slot is changing)
                using var transaction = dto.SlotId.HasValue && existingAppointment.SlotId != dto.SlotId.Value
                    ? await _context.Database.BeginTransactionAsync()
                    : null;

                try
                {
                    if (dto.CustomerId.HasValue)
                        existingAppointment.CustomerId = dto.CustomerId.Value;

                    if (dto.VehicleId.HasValue)
                        existingAppointment.VehicleId = dto.VehicleId.Value;

                    if (dto.CenterId.HasValue)
                        existingAppointment.CenterId = dto.CenterId.Value;

                    if (dto.SlotId.HasValue)
                        existingAppointment.SlotId = dto.SlotId.Value;

                    if (dto.AppointmentDate.HasValue)
                        existingAppointment.AppointmentDate = dto.AppointmentDate.Value;

                    // Status is blocked above - use dedicated endpoints

                    if (dto.Notes != null)
                        existingAppointment.Notes = dto.Notes;

                    if (dto.AssignedTechnicianId.HasValue)
                        existingAppointment.AssignedTechnicianId = dto.AssignedTechnicianId.Value;

                    existingAppointment.UpdatedAt = DateTime.UtcNow;

                    var updatedAppointment = await _appointmentDao.UpdateAppointmentAsync(existingAppointment);

                    // Commit transaction if slot was changed
                    if (transaction != null)
                        await transaction.CommitAsync();

                    // Reload appointment with includes for detailed response
                    var appointmentWithDetails = await _appointmentDao.GetAppointmentByIdAsync(updatedAppointment.AppointmentId);

                    var updatedDto = MapToResponseDto(appointmentWithDetails!, includeDetails: true);
                    return Ok(new ApiResponse<AppointmentResponseDto>(200, "Success", "Appointment updated successfully.", data: updatedDto));
                }
                catch
                {
                    if (transaction != null)
                        await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
        [HttpPut("{id}/assign-technician")]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> AssignTechnician(int id, [FromQuery] int technicianId)
        {
            try
            {
                // Get appointment để check center access
                var existingAppointment = await _appointmentDao.GetAppointmentByIdAsync(id);
                if (existingAppointment == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Appointment not found."));

                // Validation: Technician exists and has Technician role
                var technician = await _userDao.GetUserByIdAsync(technicianId);
                if (technician == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", $"User with ID {technicianId} not found."));

                if (technician.Role != UserRole.Technician.ToString())
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "Assigned user must have Technician role."));

                // Validation: Technician is Active
                if (technician.Status != UserStatus.Active.ToString())
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "Technician account is not active. Cannot assign."));

                var technicianEmployee = await _employeeDao.GetEmployeeByIdAsync(technicianId);
                if (technicianEmployee == null)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "Technician does not have an associated employee record."));

                // Validation: Technician belongs to appointment's center
                if (technicianEmployee.CenterId != existingAppointment.CenterId)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "Technician does not belong to the appointment's service center."));

                // Center restriction: Staff chỉ assign technician cho appointment tại center của họ
                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (userRole == UserRole.Staff.ToString())
                {
                    if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                        return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                    var (employee, error) = await GetCurrentEmployeeAsync(currentUserId, userRole);
                    if (error != null) return error;

                    // Check appointment thuộc center của staff
                    if (existingAppointment.CenterId != employee!.CenterId)
                    {
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                            $"You can only assign technicians to appointments at your own service center (Center ID: {employee.CenterId})."));
                    }
                }

                var updatedAppointment = await _appointmentDao.AssignTechnicianAsync(id, technicianId);
                var dto = MapToResponseDto(updatedAppointment);
                return Ok(new ApiResponse<AppointmentResponseDto>(200, "Success", "Technician assigned successfully.", data: dto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
        [HttpPut("{id}/cancel")]
        [Authorize(Roles = "Customer,Staff,Admin")]
        public async Task<IActionResult> CancelAppointment(int id)
        {
            try
            {
                // Get current user
                var (success, currentUser, errorResponse) = await GetCurrentUserAsync();
                if (!success) return errorResponse!;

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var appointment = await _context.Appointments
                        .Include(a => a.Slot)
                        .Include(a => a.Customer)
                        .FirstOrDefaultAsync(a => a.AppointmentId == id);

                    if (appointment == null)
                        return NotFound(new ApiResponse<object>(404, "NotFound", "Appointment not found."));

                    // Authorization check: Customer chỉ cancel appointment của mình
                    var accessError = ValidateCustomerAccess(currentUser!, appointment.CustomerId, "cancel");
                    if (accessError != null) return accessError;

                    // Center restriction: Staff chỉ cancel appointment tại center của họ
                    if (currentUser!.Role == UserRole.Staff.ToString())
                    {
                        var centerError = await ValidateCenterAccessAsync(appointment.CenterId, currentUser.Role, currentUser.UserId);
                        if (centerError != null) return centerError;
                    }

                    // Business rule: Không cancel nếu đã InProgress hoặc Completed
                    if (appointment.Status == AppointmentStatus.InProgress.ToString())
                    {
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            "Cannot cancel appointment that is in progress. Please contact staff for assistance."));
                    }

                    if (appointment.Status == AppointmentStatus.Completed.ToString())
                    {
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            "Cannot cancel completed appointment."));
                    }

                    // Kiểm tra đã cancelled chưa
                    if (appointment.Status == AppointmentStatus.Cancelled.ToString())
                    {
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            "Appointment has already been cancelled."));
                    }

                    // Update status
                    appointment.Status = AppointmentStatus.Cancelled.ToString();
                    appointment.UpdatedAt = DateTime.UtcNow;

                    // Free slot
                    if (appointment.Slot != null)
                    {
                        appointment.Slot.IsAvailable = true;
                    }

                    // Cancel associated work order
                    var workOrder = await _context.WorkOrders
                        .FirstOrDefaultAsync(w => w.AppointmentId == id);
                    if (workOrder != null)
                    {
                        workOrder.Status = WorkOrderStatus.Cancelled.ToString();
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Send cancellation email
                    var cancelledDto = MapToResponseDto(appointment);
                    TaskHelper.FireAndForget(
                        cancelledDto,
                        appointment.Customer?.Email,
                        _emailService.SendChangeInfoAppointmentEmailAsync
                    );

                    return Ok(new ApiResponse<object>(200, "Success", "Appointment cancelled successfully."));
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
    }
}