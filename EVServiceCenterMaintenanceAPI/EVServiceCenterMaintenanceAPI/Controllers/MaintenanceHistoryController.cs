using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
using EVServiceCenterMaintenanceAPI.Services;
using EVServiceCenterMaintenanceAPI.Utils;
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
        private readonly EmailService _emailService;
        private readonly EmployeeDao _employeeDao;

        public MaintenanceHistoryController(MaintenanceHistoryDao maintenanceHistoryDao, EvserviceCenterDbContext context, EmailService emailService, EmployeeDao employeeDao)
        {
            _maintenanceHistoryDao = maintenanceHistoryDao;
            _context = context;
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
        /// Validate foreign key relationships for maintenance history creation
        /// </summary>
        private async Task<IActionResult?> ValidateMaintenanceHistoryDataAsync(MaintenanceHistoryCreateRequestDto dto)
        {
            // 1. Validate Vehicle exists and is active
            var vehicle = await _context.Vehicles.FindAsync(dto.VehicleId);
            if (vehicle == null)
                return NotFound(new ApiResponse<object>(404, "NotFound", "Vehicle not found."));

            if (vehicle.Status != VehicleStatus.Active.ToString())
                return BadRequest(new ApiResponse<object>(400, "BadRequest", "Vehicle is not active."));

            // 2. Validate Service exists and is active
            var service = await _context.Services.FindAsync(dto.ServiceId);
            if (service == null)
                return NotFound(new ApiResponse<object>(404, "NotFound", "Service not found."));

            if (service.Status != ServiceStatus.Active.ToString())
                return BadRequest(new ApiResponse<object>(400, "BadRequest", "Service is not active."));

            // 3. Validate WorkOrder exists and belongs to the correct Vehicle
            var workOrder = await _context.WorkOrders
                .Include(wo => wo.Center)
                .Include(wo => wo.AppointmentServices)
                .FirstOrDefaultAsync(wo => wo.WorkOrderId == dto.WorkOrderId);

            if (workOrder == null)
                return NotFound(new ApiResponse<object>(404, "NotFound", "Work order not found."));

            // CRITICAL: Check WorkOrder.VehicleId matches dto.VehicleId
            if (workOrder.VehicleId != dto.VehicleId)
                return BadRequest(new ApiResponse<object>(400, "BadRequest",
                    "Work order does not belong to the specified vehicle."));

            // CRITICAL: Check Service exists in WorkOrder's AppointmentServices
            var serviceExistsInWorkOrder = workOrder.AppointmentServices
                .Any(aps => aps.ServiceId == dto.ServiceId);

            if (!serviceExistsInWorkOrder)
                return BadRequest(new ApiResponse<object>(400, "BadRequest",
                    "Service is not included in the specified work order."));

            // 4. Validate Mileage logic
            if (dto.MileageAtMaintenance.HasValue)
            {
                if (vehicle.CurrentMileage.HasValue &&
                    dto.MileageAtMaintenance.Value < vehicle.CurrentMileage.Value)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        $"Mileage at maintenance ({dto.MileageAtMaintenance.Value}) cannot be less than vehicle's current mileage ({vehicle.CurrentMileage.Value})."));
            }

            return null; // All validations passed
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
        [Authorize(Roles = "Staff,Technician,Admin")]
        public async Task<IActionResult> CreateMaintenanceHistory([FromBody] MaintenanceHistoryCreateRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(kvp => !string.IsNullOrEmpty(kvp.Key) && kvp.Key != "id" && kvp.Value?.Errors?.Count > 0)
                        .SelectMany(kvp => kvp.Value!.Errors.Select(e => $"{kvp.Key}: {e.ErrorMessage}"))
                        .ToList();
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", string.Join("; ", errors)));
                }

                // Validate MaintenanceDate
                if (dto.MaintenanceDate > DateTime.UtcNow.AddMinutes(30))
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "Maintenance date cannot be in the future."));

                if (dto.MaintenanceDate < DateTime.UtcNow.AddYears(-5))
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "Maintenance date cannot be more than 5 years in the past."));

                // Center validation for Staff/Technician
                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (userRole == UserRole.Staff.ToString() || userRole == UserRole.Technician.ToString())
                {
                    if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                        return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                    var (employee, employeeError) = await GetCurrentEmployeeAsync(currentUserId, userRole);
                    if (employeeError != null) return employeeError;

                    // Check if WorkOrder belongs to employee's center
                    var workOrder = await _context.WorkOrders
                        .Where(wo => wo.WorkOrderId == dto.WorkOrderId)
                        .Select(wo => new { wo.WorkOrderId, wo.CenterId })
                        .FirstOrDefaultAsync();

                    if (workOrder != null && workOrder.CenterId != employee!.CenterId)
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                            $"{userRole} can only create maintenance histories for work orders at their center (Center ID: {employee.CenterId})."));

                    // Additional check for Technician: Must be assigned to at least one service in the WorkOrder
                    if (userRole == UserRole.Technician.ToString())
                    {
                        var isAssignedToWorkOrder = await _context.AppointmentServices
                            .AnyAsync(aps => aps.WorkOrderId == dto.WorkOrderId &&
                                            aps.AssignedTechnicianId == currentUserId);

                        if (!isAssignedToWorkOrder)
                            return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                                "Technician can only create maintenance histories for work orders they are assigned to."));
                    }
                }

                // Validate foreign key relationships (Vehicle, Service, WorkOrder consistency)
                var validationError = await ValidateMaintenanceHistoryDataAsync(dto);
                if (validationError != null)
                    return validationError;

                // Check for duplicate maintenance history within the SAME WorkOrder
                // Allow duplicate service if it's from a different WorkOrder (customer books again)
                var recentHistory = await _context.MaintenanceHistories
                    .Where(h => h.VehicleId == dto.VehicleId &&
                                h.ServiceId == dto.ServiceId &&
                                h.WorkOrderId == dto.WorkOrderId &&
                                h.MaintenanceDate >= dto.MaintenanceDate.AddHours(-24) &&
                                h.MaintenanceDate <= dto.MaintenanceDate.AddHours(24))
                    .FirstOrDefaultAsync();

                if (recentHistory != null)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        "A similar maintenance history already exists for this work order within 24 hours."));
                var history = new MaintenanceHistory
                {
                    VehicleId = dto.VehicleId,
                    WorkOrderId = dto.WorkOrderId,
                    ServiceId = dto.ServiceId,
                    MaintenanceDate = dto.MaintenanceDate,
                    Description = dto.Description,
                    Notes = dto.Notes,
                    Cost = dto.Cost,
                    MileageAtMaintenance = dto.MileageAtMaintenance,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _maintenanceHistoryDao.CreateMaintenanceHistoryAsync(history);
                var createdHistory = await _maintenanceHistoryDao.GetMaintenanceHistoryByIdAsync(history.HistoryId);
                if (createdHistory == null)
                    return StatusCode(500, new ApiResponse<object>(500, "Error", "Failed to retrieve created maintenance history."));

                var createdDto = new MaintenanceHistoryResponseDto
                {
                    HistoryId = createdHistory.HistoryId,
                    VehicleId = createdHistory.VehicleId,
                    WorkOrderId = createdHistory.WorkOrderId,
                    MaintenanceDate = createdHistory.MaintenanceDate,
                    Description = createdHistory.Description,
                    Notes = createdHistory.Notes,
                    Cost = createdHistory.Cost,
                    MileageAtMaintenance = createdHistory.MileageAtMaintenance,
                    CreatedAt = createdHistory.CreatedAt,
                    UpdatedAt = createdHistory.UpdatedAt,
                    ServiceDetails = createdHistory.Service == null ? null : new ServiceResponseDto
                    {
                        ServiceId = createdHistory.Service.ServiceId,
                        ServiceName = createdHistory.Service.ServiceName,
                        Description = createdHistory.Service.Description,
                        BasePrice = createdHistory.Service.BasePrice,
                        EstimatedTime = createdHistory.Service.EstimatedTime,
                        Status = Enum.Parse<ServiceStatus>(createdHistory.Service.Status),
                        ReminderIntervalDays = createdHistory.Service.ReminderIntervalDays ?? 0,
                        ReminderMileage = createdHistory.Service.ReminderMileage ?? 0,
                        Notes = createdHistory.Service.Notes,
                        CreatedAt = createdHistory.Service.CreatedAt,
                        UpdatedAt = createdHistory.Service.UpdatedAt
                    },
                    VehicleDetails = createdHistory.Vehicle == null ? null : new VehicleResponeDto
                    {
                        VehicleId = createdHistory.Vehicle.VehicleId,
                        CustomerId = createdHistory.Vehicle.CustomerId,
                        Model = createdHistory.Vehicle.Model,
                        VIN = createdHistory.Vehicle.Vin,
                        ManufactureYear = createdHistory.Vehicle.ManufactureYear,
                        CurrentMileage = createdHistory.Vehicle.CurrentMileage ?? 0,
                        LastMaintenanceDate = createdHistory.Vehicle.LastMaintenanceDate,
                        Color = createdHistory.Vehicle.Color,
                        Plate = createdHistory.Vehicle.Plate,
                        CreatedAt = createdHistory.Vehicle.CreatedAt,
                        UpdatedAt = createdHistory.Vehicle.UpdatedAt
                    },
                    WorkOrderDetails = createdHistory.WorkOrder == null ? null : new WorkOrderResponseDto
                    {
                        WorkOrderId = createdHistory.WorkOrder.WorkOrderId,
                        CenterId = createdHistory.WorkOrder.CenterId,
                        CustomerId = createdHistory.WorkOrder.CustomerId,
                        VehicleId = createdHistory.WorkOrder.VehicleId,
                        CreatedByStaffId = createdHistory.WorkOrder.CreatedByStaffId,
                        AppointmentId = createdHistory.WorkOrder.AppointmentId,
                        Status = Enum.Parse<WorkOrderStatus>(createdHistory.WorkOrder.Status),
                        CheckInAt = createdHistory.WorkOrder.CheckInAt,
                        CheckOutAt = createdHistory.WorkOrder.CheckOutAt,
                        OdometerKm = createdHistory.WorkOrder.OdometerKm,
                        Notes = createdHistory.WorkOrder.Notes
                    },
                    PartUsageDetails = createdHistory.PartUsages?.Select(pu =>
                    {
                        var totalCost = pu.UnitCostPrice * pu.QuantityUsed;
                        var totalPrice = pu.UnitPrice * pu.QuantityUsed;
                        var profit = totalPrice - totalCost;
                        var profitMargin = totalPrice > 0 ? Math.Round(profit / totalPrice * 100, 2) : 0;

                        return new PartUsageResponseDto
                        {
                            UsageId = pu.UsageId,
                            HistoryId = pu.HistoryId,
                            PartId = pu.PartId,
                            QuantityUsed = pu.QuantityUsed,
                            UnitCostPrice = pu.UnitCostPrice,
                            UnitPrice = pu.UnitPrice,
                            PartName = pu.Part.PartName,
                            PartDescription = pu.Part.Description,
                            TotalCost = totalCost,
                            TotalPrice = totalPrice,
                            Profit = profit,
                            ProfitMargin = profitMargin,
                            WorkOrderId = createdHistory.WorkOrderId,
                            VehicleId = createdHistory.VehicleId
                        };
                    }).ToList()
                };

                // Send email notification only when all services in workorder have maintenance history
                var customerEmail = createdHistory.WorkOrder?.Customer?.Email;
                if (!string.IsNullOrEmpty(customerEmail) && createdHistory.WorkOrderId.HasValue)
                {
                    // Get all services in this workorder
                    var workOrderServices = await _context.AppointmentServices
                        .Where(aps => aps.WorkOrderId == createdHistory.WorkOrderId.Value)
                        .Select(aps => aps.ServiceId)
                        .Distinct()
                        .ToListAsync();

                    // Get all services that already have maintenance history for this workorder
                    var servicesWithHistory = await _context.MaintenanceHistories
                        .Where(mh => mh.WorkOrderId == createdHistory.WorkOrderId.Value)
                        .Select(mh => mh.ServiceId)
                        .Distinct()
                        .ToListAsync();

                    // Only send email if all services have maintenance history
                    if (workOrderServices.Count > 0 && workOrderServices.All(serviceId => servicesWithHistory.Contains(serviceId)))
                    {
                        // Get all maintenance histories for this workorder to send comprehensive email
                        var allHistories = await _context.MaintenanceHistories
                            .Where(mh => mh.WorkOrderId == createdHistory.WorkOrderId.Value)
                            .Include(mh => mh.Service)
                            .Include(mh => mh.Vehicle)
                            .Include(mh => mh.WorkOrder)
                                .ThenInclude(wo => wo!.Customer)
                            .Include(mh => mh.WorkOrder)
                                .ThenInclude(wo => wo!.Center)
                            .Include(mh => mh.PartUsages)
                                .ThenInclude(pu => pu.Part)
                            .ToListAsync();

                        var allHistoriesDto = allHistories.Select(h => new MaintenanceHistoryResponseDto
                        {
                            HistoryId = h.HistoryId,
                            VehicleId = h.VehicleId,
                            WorkOrderId = h.WorkOrderId,
                            ServiceId = h.ServiceId ?? 0,
                            MaintenanceDate = h.MaintenanceDate,
                            Description = h.Description,
                            Notes = h.Notes,
                            Cost = h.Cost,
                            MileageAtMaintenance = h.MileageAtMaintenance,
                            CreatedAt = h.CreatedAt,
                            UpdatedAt = h.UpdatedAt,
                            ServiceDetails = h.Service == null ? null : new ServiceResponseDto
                            {
                                ServiceId = h.Service.ServiceId,
                                ServiceName = h.Service.ServiceName,
                                Description = h.Service.Description,
                                BasePrice = h.Service.BasePrice,
                                EstimatedTime = h.Service.EstimatedTime,
                                Status = Enum.Parse<ServiceStatus>(h.Service.Status),
                                ReminderIntervalDays = h.Service.ReminderIntervalDays ?? 0,
                                ReminderMileage = h.Service.ReminderMileage ?? 0,
                                Notes = h.Service.Notes,
                                CreatedAt = h.Service.CreatedAt,
                                UpdatedAt = h.Service.UpdatedAt
                            },
                            VehicleDetails = h.Vehicle == null ? null : new VehicleResponeDto
                            {
                                VehicleId = h.Vehicle.VehicleId,
                                CustomerId = h.Vehicle.CustomerId,
                                Model = h.Vehicle.Model,
                                VIN = h.Vehicle.Vin,
                                ManufactureYear = h.Vehicle.ManufactureYear,
                                CurrentMileage = h.Vehicle.CurrentMileage ?? 0,
                                LastMaintenanceDate = h.Vehicle.LastMaintenanceDate,
                                Color = h.Vehicle.Color,
                                Plate = h.Vehicle.Plate,
                                CreatedAt = h.Vehicle.CreatedAt,
                                UpdatedAt = h.Vehicle.UpdatedAt
                            },
                            WorkOrderDetails = h.WorkOrder == null ? null : new WorkOrderResponseDto
                            {
                                WorkOrderId = h.WorkOrder.WorkOrderId,
                                CenterId = h.WorkOrder.CenterId,
                                CustomerId = h.WorkOrder.CustomerId,
                                VehicleId = h.WorkOrder.VehicleId,
                                CreatedByStaffId = h.WorkOrder.CreatedByStaffId,
                                AppointmentId = h.WorkOrder.AppointmentId,
                                Status = Enum.Parse<WorkOrderStatus>(h.WorkOrder.Status),
                                CheckInAt = h.WorkOrder.CheckInAt,
                                CheckOutAt = h.WorkOrder.CheckOutAt,
                                OdometerKm = h.WorkOrder.OdometerKm,
                                Notes = h.WorkOrder.Notes,
                                CenterDetails = h.WorkOrder.Center == null ? null : new ServiceCenterResponseDto
                                {
                                    CenterId = h.WorkOrder.Center.CenterId,
                                    CenterName = h.WorkOrder.Center.CenterName,
                                    Address = h.WorkOrder.Center.Address,
                                    Phone = h.WorkOrder.Center.Phone,
                                    Email = h.WorkOrder.Center.Email,
                                    Status = Enum.Parse<ServiceCenterStatus>(h.WorkOrder.Center.Status)
                                },
                                CustomerDetails = h.WorkOrder.Customer == null ? null : new UserResponseDto
                                {
                                    UserId = h.WorkOrder.Customer.UserId,
                                    FullName = h.WorkOrder.Customer.FullName,
                                    Email = h.WorkOrder.Customer.Email,
                                    Phone = h.WorkOrder.Customer.Phone,
                                    Status = Enum.Parse<UserStatus>(h.WorkOrder.Customer.Status)
                                }
                            },
                            PartUsageDetails = h.PartUsages?.Select(pu =>
                            {
                                var totalCost = pu.UnitCostPrice * pu.QuantityUsed;
                                var totalPrice = pu.UnitPrice * pu.QuantityUsed;
                                var profit = totalPrice - totalCost;
                                var profitMargin = totalPrice > 0 ? Math.Round(profit / totalPrice * 100, 2) : 0;

                                return new PartUsageResponseDto
                                {
                                    UsageId = pu.UsageId,
                                    HistoryId = pu.HistoryId,
                                    PartId = pu.PartId,
                                    QuantityUsed = pu.QuantityUsed,
                                    UnitCostPrice = pu.UnitCostPrice,
                                    UnitPrice = pu.UnitPrice,
                                    PartName = pu.Part?.PartName,
                                    PartDescription = pu.Part?.Description,
                                    TotalCost = totalCost,
                                    TotalPrice = totalPrice,
                                    Profit = profit,
                                    ProfitMargin = profitMargin,
                                    WorkOrderId = h.WorkOrderId,
                                    VehicleId = h.VehicleId
                                };
                            }).ToList()
                        }).ToList();

                        // Send email with all maintenance histories
                        TaskHelper.FireAndForget(allHistoriesDto, customerEmail, _emailService.SendMaintenanceHistoriesCreatedEmailAsync);
                    }
                }

                return CreatedAtAction(nameof(GetMaintenanceHistory), new { id = createdHistory.HistoryId },
                    new ApiResponse<MaintenanceHistoryResponseDto>(201, "Created", "Maintenance history created successfully.", data: createdDto));
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
