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
        public AppointmentController(AppointmentDao appointmentDao, WorkOrderDao workOrderDao, EmailService email, EvserviceCenterDbContext context, PayOSService payOSService)
        {
            _appointmentDao = appointmentDao;
            _workOrderDao = workOrderDao;
            _emailService = email;
            _context = context;
            _payOSService = payOSService;
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
            var vehicle = await _context.Vehicles.FindAsync(vehicleId);
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

                // Validation: Center exists
                var centerError = await ValidateCenterExistsAsync(dto.CenterId);
                if (centerError != null) return centerError;

                // Validation: Vehicle belongs to customer
                var vehicleError = await ValidateVehicleBelongsToCustomerAsync(dto.VehicleId, dto.CustomerId);
                if (vehicleError != null) return vehicleError;

                // Validation: AppointmentDate không được trong quá khứ (theo giờ Vietnam)
                var todayVietnam = TimeZoneHelper.TodayInVietnam;
                var appointmentDateVN = dto.AppointmentDate.ConvertToVietnamTime();
                if (appointmentDateVN.Date < todayVietnam)
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "Appointment date cannot be in the past."));
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Validate and lock slot
                    var (slotSuccess, slot, slotError) = await ValidateAndLockSlotAsync(dto.SlotId, dto.CenterId);
                    if (!slotSuccess) return slotError!;

                    // Validate services and calculate cost
                    var serviceIds = dto.ServiceIds ?? [];
                    var (servicesSuccess, totalCost, servicesError) = await ValidateServicesAndCalculateCostAsync(serviceIds);
                    if (!servicesSuccess) return servicesError!;

                    // Mark slot as unavailable
                    slot!.IsAvailable = false;

                    // Create appointment
                    var appointment = new Appointment
                    {
                        CustomerId = dto.CustomerId,
                        VehicleId = dto.VehicleId,
                        CenterId = dto.CenterId,
                        SlotId = dto.SlotId,
                        AppointmentDate = dto.AppointmentDate,
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

                    // Map to response DTO
                    var createdDto = MapToResponseDto(createdAppointment);
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

                // Validation: AppointmentDate không được trong quá khứ (theo giờ Vietnam)
                var todayVietnam = TimeZoneHelper.TodayInVietnam;
                var appointmentDateVN = dto.AppointmentDate.ConvertToVietnamTime();
                if (appointmentDateVN.Date < todayVietnam)
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "Appointment date cannot be in the past."));
                }

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
                    if (!slotSuccess) return slotError!;

                    // Validate services and calculate cost
                    var (servicesSuccess, totalCost, servicesError) = await ValidateServicesAndCalculateCostAsync(serviceIds);
                    if (!servicesSuccess) return servicesError!;

                    // Mark slot as unavailable
                    slot!.IsAvailable = false;

                    // Create appointment
                    var appointment = new Appointment
                    {
                        CustomerId = dto.CustomerId,
                        VehicleId = dto.VehicleId,
                        CenterId = dto.CenterId,
                        SlotId = dto.SlotId,
                        AppointmentDate = dto.AppointmentDate,
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
                    var paymentResult = await _payOSService.CreatePaymentLink(
                        createdWorkOrder.WorkOrderId,
                        totalCost,
                        "Payment for EV",
                        items,
                        cancelUrl,
                        successUrl
                    );

                    await transaction.CommitAsync();

                    // Map to response DTO
                    var createdDto = MapToResponseDto(createdAppointment);
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
                // Validate id matches dto
                if (id != dtoUpdate.AppointmentId)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Appointment ID mismatch."));

                // Update status (DAO returns updated appointment with includes)
                var updatedAppointment = await _appointmentDao.UpdateAppointmentStatusAsync(dtoUpdate.AppointmentId, dtoUpdate.Status);
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
                var dtos = appointments.Select(a => MapToResponseDto(a)).ToList();
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
                var dtos = appointments.Select(a => MapToResponseDto(a)).ToList();
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

                // Validate id matches dto
                if (id != dto.AppointmentId)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Appointment ID mismatch."));

                var existingAppointment = await _context.Appointments.FindAsync(id);
                if (existingAppointment == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Appointment not found."));

                // Validation: Center exists
                var centerError = await ValidateCenterExistsAsync(dto.CenterId);
                if (centerError != null) return centerError;

                // Validation: Vehicle belongs to customer
                var vehicleError = await ValidateVehicleBelongsToCustomerAsync(dto.VehicleId, dto.CustomerId);
                if (vehicleError != null) return vehicleError;

                // Validation: AppointmentDate không được trong quá khứ (theo giờ Vietnam)
                var todayVietnam = TimeZoneHelper.TodayInVietnam;
                var appointmentDateVN = dto.AppointmentDate.ConvertToVietnamTime();
                if (appointmentDateVN.Date < todayVietnam)
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "Appointment date cannot be in the past."));
                }

                // Build appointment object for update
                var appointment = new Appointment
                {
                    AppointmentId = dto.AppointmentId,
                    CustomerId = dto.CustomerId,
                    VehicleId = dto.VehicleId,
                    CenterId = dto.CenterId,
                    SlotId = dto.SlotId,
                    AppointmentDate = dto.AppointmentDate,
                    Status = dto.Status.ToString(),
                    Notes = dto.Notes,
                    AssignedTechnicianId = dto.AssignedTechnicianId,
                    Amount = existingAppointment.Amount,
                    UpdatedAt = DateTime.UtcNow
                };

                var updatedAppointment = await _appointmentDao.UpdateAppointmentAsync(appointment);
                var updatedDto = MapToResponseDto(updatedAppointment);
                return Ok(new ApiResponse<AppointmentResponseDto>(200, "Success", "Appointment updated successfully.", data: updatedDto));
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