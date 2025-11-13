using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PartUsageController : ControllerBase
    {
        private readonly PartUsageDao _partUsageDao;
        private readonly EvserviceCenterDbContext _context;
        private readonly UserDao _userDao;
        private readonly EmployeeDao _employeeDao;
        private readonly ILogger<PartUsageController> _logger;

        public PartUsageController(PartUsageDao partUsageDao, EvserviceCenterDbContext context, UserDao userDao, EmployeeDao employeeDao, ILogger<PartUsageController> logger)
        {
            _partUsageDao = partUsageDao;
            _context = context;
            _userDao = userDao;
            _employeeDao = employeeDao;
            _logger = logger;
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Staff,Technician")]
        public async Task<IActionResult> CreatePartUsage([FromBody] PartUsageCreateRequestDto dto)
        {
            try
            {
                // Validate ModelState
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(kvp => kvp.Value?.Errors?.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>());
                    return BadRequest(new ApiResponse<object>(400, "ValidationError", "Invalid input data.", errors));
                }

                // Center restriction: Staff/Technician chỉ được tạo part usage cho parts tại center của họ
                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (userRole == UserRole.Staff.ToString() || userRole == UserRole.Technician.ToString())
                {
                    if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                        return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                    var partToCheck = await _context.Parts.FindAsync(dto.PartId);
                    if (partToCheck == null)
                        return NotFound(new ApiResponse<object>(404, "NotFound", $"Part with ID {dto.PartId} not found."));

                    var (_, employee, error) = await GetCurrentUserAndEmployeeAsync(currentUserId);
                    if (error != null) return error;

                    if (partToCheck.CenterId != employee!.CenterId)
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                            $"{userRole} can only create part usage for parts from their own service center (Center ID: {employee.CenterId})."));
                }

                // Check stock availability first
                var (isAvailable, message, part) = await _partUsageDao.CheckStockAvailabilityAsync(dto.PartId, dto.QuantityUsed);

                if (!isAvailable)
                {
                    _logger.LogWarning("Stock check failed for PartId {PartId}: {Message}", dto.PartId, message);
                    return BadRequest(new ApiResponse<object>(400, "StockNotAvailable", message));
                }

                if (part == null)
                {
                    return NotFound(new ApiResponse<object>(404, "NotFound", $"Part with ID {dto.PartId} not found."));
                }

                var unitCostPrice = part.CostPrice;
                var unitPrice = part.Price;

                if (dto.UnitCostPrice.HasValue && dto.UnitCostPrice.Value != unitCostPrice)
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        $"UnitCostPrice mismatch. Expected {unitCostPrice}, received {dto.UnitCostPrice.Value}."));
                }

                if (dto.UnitPrice.HasValue && dto.UnitPrice.Value != unitPrice)
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        $"UnitPrice mismatch. Expected {unitPrice}, received {dto.UnitPrice.Value}."));
                }

                // Create PartUsage entity
                var partUsage = new PartUsage
                {
                    HistoryId = dto.HistoryId,
                    PartId = dto.PartId,
                    QuantityUsed = dto.QuantityUsed,
                    UnitCostPrice = unitCostPrice,
                    UnitPrice = unitPrice
                };

                // Create (DAO handles validation, stock check, and stock deduction)
                var createdPartUsage = await _partUsageDao.CreatePartUsageAsync(partUsage);

                // Build response DTO
                var responseDto = MapToResponseDto(createdPartUsage);

                _logger.LogInformation(
                    "Part Usage created: UsageId={UsageId}, HistoryId={HistoryId}, Part={PartName}, Quantity={Quantity}, " +
                    "TotalPrice={TotalPrice} VND. {StockWarning}",
                    createdPartUsage.UsageId,
                    createdPartUsage.HistoryId,
                    createdPartUsage.Part?.PartName,
                    createdPartUsage.QuantityUsed,
                    responseDto.TotalPrice,
                    message.Contains("LOW") ? $"WARNING: {message}" : "");

                return CreatedAtAction(
                    nameof(GetPartUsage),
                    new { id = createdPartUsage.UsageId },
                    new ApiResponse<PartUsageResponseDto>(201, "Created", "Part usage created successfully.", data: responseDto));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error creating Part Usage");
                return NotFound(new ApiResponse<object>(404, "NotFound", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Business logic error creating Part Usage");
                return BadRequest(new ApiResponse<object>(400, "BadRequest", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Part Usage");
                return StatusCode(500, new ApiResponse<object>(500, "InternalServerError", "An error occurred while creating part usage."));
            }
        }


        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetPartUsage(int id)
        {
            try
            {
                var partUsage = await _partUsageDao.GetPartUsageByIdAsync(id);

                if (partUsage == null)
                {
                    return NotFound(new ApiResponse<object>(404, "NotFound", $"Part usage with ID {id} not found."));
                }

                // Authorization checks
                var (userIdClaim, userRole) = GetCurrentUserInfo();

                // Customer chỉ xem part usage của mình
                if (userRole == UserRole.Customer.ToString() && int.TryParse(userIdClaim, out int userId))
                {
                    var customerError = await ValidateCustomerAccessAsync(partUsage.History?.WorkOrder?.CustomerId, userId);
                    if (customerError != null) return customerError;
                }

                // Staff/Technician chỉ xem part usage tại center của họ
                if (userRole == UserRole.Staff.ToString() || userRole == UserRole.Technician.ToString())
                {
                    if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                        return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                    var centerError = await ValidateCenterAccessAsync(partUsage.Part?.CenterId, userRole, currentUserId);
                    if (centerError != null) return centerError;
                }

                var responseDto = MapToResponseDto(partUsage);

                return Ok(new ApiResponse<PartUsageResponseDto>(200, "Success", "Part usage retrieved successfully.", data: responseDto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Part Usage {UsageId}", id);
                return StatusCode(500, new ApiResponse<object>(500, "InternalServerError", "An error occurred while retrieving part usage."));
            }
        }

        [HttpGet("history/{historyId}")]
        [Authorize]
        public async Task<IActionResult> GetPartUsagesByHistory(int historyId)
        {
            try
            {
                var partUsages = await _partUsageDao.GetPartUsagesByHistoryIdAsync(historyId);

                if (partUsages == null || partUsages.Count == 0)
                {
                    return NotFound(new ApiResponse<object>(404, "NotFound", $"No part usages found for MaintenanceHistory {historyId}."));
                }

                // Authorization checks
                var (userIdClaim, userRole) = GetCurrentUserInfo();

                // Customer chỉ xem part usage của mình
                if (userRole == UserRole.Customer.ToString() && int.TryParse(userIdClaim, out int userId))
                {
                    var firstPartUsage = partUsages.First();
                    var customerError = await ValidateCustomerAccessAsync(firstPartUsage.History?.WorkOrder?.CustomerId, userId);
                    if (customerError != null) return customerError;
                }

                // Staff/Technician chỉ xem part usage tại center của họ
                if (userRole == UserRole.Staff.ToString() || userRole == UserRole.Technician.ToString())
                {
                    if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                        return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                    var (_, employee, error) = await GetCurrentUserAndEmployeeAsync(currentUserId);
                    if (error != null) return error;

                    // Check if any part usage is from different center
                    if (employee != null)
                    {
                        var hasDifferentCenter = partUsages.Any(pu => pu.Part?.CenterId != employee.CenterId);
                        if (hasDifferentCenter)
                            return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                                $"{userRole} can only view part usage from their own service center (Center ID: {employee.CenterId})."));
                    }
                }

                var (totalCost, totalPrice) = await _partUsageDao.GetPartUsagesTotalAsync(historyId);

                var dtos = partUsages.Select(MapToResponseDto).ToList();

                var summary = new PartUsageSummaryDto
                {
                    HistoryId = historyId,
                    TotalPartsUsed = partUsages.Count,
                    TotalCost = totalCost,
                    TotalPrice = totalPrice,
                    TotalProfit = totalPrice - totalCost,
                    PartUsages = dtos
                };

                return Ok(new ApiResponse<PartUsageSummaryDto>(200, "Success", "Part usages retrieved successfully.", data: summary));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Part Usages for HistoryId {HistoryId}", historyId);
                return StatusCode(500, new ApiResponse<object>(500, "InternalServerError", "An error occurred while retrieving part usages."));
            }
        }

        [HttpGet("part/{partId}")]
        [Authorize(Roles = "Admin,Staff,Technician")]
        public async Task<IActionResult> GetPartUsagesByPart(int partId)
        {
            try
            {
                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                // Get Part to check CenterId
                var part = await _context.Parts.FindAsync(partId);
                if (part == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", $"Part with ID {partId} not found."));

                // Staff/Technician chỉ được xem part usage tại center của họ
                if (userRole == UserRole.Staff.ToString() || userRole == UserRole.Technician.ToString())
                {
                    var centerError = await ValidateCenterAccessAsync(part.CenterId, userRole, currentUserId);
                    if (centerError != null) return centerError;
                }

                var partUsages = await _partUsageDao.GetPartUsagesByPartIdAsync(partId);
                var dtos = partUsages.Select(MapToResponseDto).ToList();

                return Ok(new ApiResponse<List<PartUsageResponseDto>>(200, "Success", "Part usages retrieved successfully.", data: dtos));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Part Usages for PartId {PartId}", partId);
                return StatusCode(500, new ApiResponse<object>(500, "InternalServerError", "An error occurred while retrieving part usages."));
            }
        }

        [HttpGet("my-parts")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetMyPartUsages()
        {
            try
            {
                var userIdClaim = User.FindFirst("UserId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int customerId))
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                var partUsages = await _partUsageDao.GetPartUsagesByCustomerIdAsync(customerId);
                var dtos = partUsages.Select(MapToResponseDto).ToList();

                // Group by WorkOrder for better organization
                var groupedByWorkOrder = dtos
                    .GroupBy(d => d.WorkOrderId)
                    .Select(g => new
                    {
                        WorkOrderId = g.Key,
                        TotalPartsUsed = g.Count(),
                        TotalCost = g.Sum(d => d.TotalCost),
                        TotalPrice = g.Sum(d => d.TotalPrice),
                        TotalProfit = g.Sum(d => d.Profit),
                        PartUsages = g.ToList()
                    })
                    .ToList();

                var totalCost = dtos.Sum(d => d.TotalCost);
                var totalPrice = dtos.Sum(d => d.TotalPrice);

                var responseData = new
                {
                    customerId = customerId,
                    totalWorkOrders = groupedByWorkOrder.Count,
                    totalPartsUsed = partUsages.Count,
                    totalCost = totalCost,
                    totalPrice = totalPrice,
                    totalProfit = totalPrice - totalCost,
                    byWorkOrder = groupedByWorkOrder
                };

                return Ok(new ApiResponse<object>(200, "Success", "Part usages retrieved successfully.", data: responseData));
            }
            catch (Exception ex)
            {
                var userIdClaim = User.FindFirst("UserId")?.Value;
                _logger.LogError(ex, "Error retrieving Part Usages for Customer {CustomerId}", userIdClaim);
                return StatusCode(500, new ApiResponse<object>(500, "InternalServerError", "An error occurred while retrieving part usages."));
            }
        }

        [HttpGet("workorder/{workOrderId}")]
        [Authorize]
        public async Task<IActionResult> GetPartUsagesByWorkOrder(int workOrderId)
        {
            try
            {
                // Authorization checks
                var (userIdClaim, userRole) = GetCurrentUserInfo();

                // Customer chỉ xem part usage của mình
                if (userRole == UserRole.Customer.ToString() && int.TryParse(userIdClaim, out int userId))
                {
                    var workOrder = await _context.WorkOrders.FindAsync(workOrderId);
                    if (workOrder == null)
                        return NotFound(new ApiResponse<object>(404, "NotFound", $"WorkOrder with ID {workOrderId} not found."));

                    var customerError = await ValidateCustomerAccessAsync(workOrder.CustomerId, userId);
                    if (customerError != null) return customerError;
                }

                // Get part usages (reuse for both authorization check and response)
                var partUsages = await _partUsageDao.GetPartUsagesByWorkOrderIdAsync(workOrderId);

                // Staff/Technician chỉ xem part usage tại center của họ
                if (userRole == UserRole.Staff.ToString() || userRole == UserRole.Technician.ToString())
                {
                    if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                        return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                    if (partUsages.Count > 0)
                    {
                        var (_, employee, error) = await GetCurrentUserAndEmployeeAsync(currentUserId);
                        if (error != null) return error;

                        // Check if any part usage is from different center
                        if (employee != null)
                        {
                            var hasDifferentCenter = partUsages.Any(pu => pu.Part?.CenterId != employee.CenterId);
                            if (hasDifferentCenter)
                                return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                                    $"{userRole} can only view part usage from their own service center (Center ID: {employee.CenterId})."));
                        }
                    }
                }
                var dtos = partUsages.Select(MapToResponseDto).ToList();

                var totalCost = dtos.Sum(d => d.TotalCost);
                var totalPrice = dtos.Sum(d => d.TotalPrice);

                var responseData = new
                {
                    workOrderId = workOrderId,
                    totalPartsUsed = partUsages.Count,
                    totalCost = totalCost,
                    totalPrice = totalPrice,
                    totalProfit = totalPrice - totalCost,
                    partUsages = dtos
                };

                return Ok(new ApiResponse<object>(200, "Success", "Part usages retrieved successfully.", data: responseData));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Part Usages for WorkOrderId {WorkOrderId}", workOrderId);
                return StatusCode(500, new ApiResponse<object>(500, "InternalServerError", "An error occurred while retrieving part usages."));
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Staff,Technician")]
        public async Task<IActionResult> UpdatePartUsage(int id, [FromBody] PartUsageUpdateRequestDto dto)
        {
            try
            {
                // Validate ModelState
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(kvp => kvp.Value?.Errors?.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>());
                    return BadRequest(new ApiResponse<object>(400, "ValidationError", "Invalid input data.", errors));
                }

                // Get existing part usage
                var existingPartUsage = await _partUsageDao.GetPartUsageByIdAsync(id);
                if (existingPartUsage == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", $"Part usage with ID {id} not found."));

                // Center restriction: Staff/Technician chỉ được update part usage tại center của họ
                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (userRole == UserRole.Staff.ToString() || userRole == UserRole.Technician.ToString())
                {
                    if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                        return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                    var centerError = await ValidateCenterAccessAsync(existingPartUsage.Part?.CenterId, userRole, currentUserId);
                    if (centerError != null) return centerError;
                }

                // Save original quantity before update (for stock adjustment calculation)
                int originalQuantity = existingPartUsage.QuantityUsed;

                // Update fields that are provided (partial update support)
                if (dto.QuantityUsed.HasValue)
                    existingPartUsage.QuantityUsed = dto.QuantityUsed.Value;

                if (dto.UnitCostPrice.HasValue)
                    existingPartUsage.UnitCostPrice = dto.UnitCostPrice.Value;

                if (dto.UnitPrice.HasValue)
                    existingPartUsage.UnitPrice = dto.UnitPrice.Value;

                // Update (DAO handles stock adjustment if quantity changed)
                var updatedPartUsage = await _partUsageDao.UpdatePartUsageAsync(existingPartUsage, originalQuantity);

                var responseDto = MapToResponseDto(updatedPartUsage);

                _logger.LogInformation(
                    "Part Usage updated: UsageId={UsageId}, New Quantity={Quantity}, TotalPrice={TotalPrice} VND",
                    updatedPartUsage.UsageId,
                    updatedPartUsage.QuantityUsed,
                    responseDto.TotalPrice);

                return Ok(new ApiResponse<PartUsageResponseDto>(200, "Success", "Part usage updated successfully.", data: responseDto));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error updating Part Usage {UsageId}", id);
                return NotFound(new ApiResponse<object>(404, "NotFound", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Business logic error updating Part Usage {UsageId}", id);
                return BadRequest(new ApiResponse<object>(400, "BadRequest", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Part Usage {UsageId}", id);
                return StatusCode(500, new ApiResponse<object>(500, "InternalServerError", "An error occurred while updating part usage."));
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeletePartUsage(int id)
        {
            try
            {
                // Admin can delete any part usage (no center restriction needed)
                var success = await _partUsageDao.DeletePartUsageAsync(id);

                if (!success)
                {
                    return NotFound(new ApiResponse<object>(404, "NotFound", $"Part usage with ID {id} not found."));
                }

                _logger.LogInformation("Part Usage deleted: UsageId={UsageId}, Stock returned to inventory", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Part Usage {UsageId}", id);
                return StatusCode(500, new ApiResponse<object>(500, "InternalServerError", "An error occurred while deleting part usage."));
            }
        }

        [HttpPost("check-stock")]
        [Authorize(Roles = "Admin,Staff,Technician")]
        public async Task<IActionResult> CheckStockAvailability([FromBody] PartStockCheckDto dto)
        {
            try
            {
                var (isAvailable, message, part) = await _partUsageDao.CheckStockAvailabilityAsync(dto.PartId, dto.RequestedQuantity);

                var response = new PartStockCheckDto
                {
                    PartId = dto.PartId,
                    PartName = part?.PartName ?? "Unknown",
                    RequestedQuantity = dto.RequestedQuantity,
                    CurrentStock = part?.QuantityInStock,
                    IsAvailable = isAvailable,
                    MinStock = part?.MinStock,
                    WillBeLowStock = part != null &&
                                     ((part.QuantityInStock ?? 0) - dto.RequestedQuantity) < (part.MinStock ?? 0),
                    Message = message
                };

                return Ok(new ApiResponse<PartStockCheckDto>(200, "Success", "Stock check completed.", data: response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking stock for PartId {PartId}", dto.PartId);
                return StatusCode(500, new ApiResponse<object>(500, "InternalServerError", "An error occurred while checking stock."));
            }
        }

        /// <summary>
        /// Helper method to get current user info
        /// </summary>
        private (string? UserIdClaim, string? UserRole) GetCurrentUserInfo()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            return (userIdClaim, userRole);
        }

        /// <summary>
        /// Helper method to check if Customer can access resource (via WorkOrder)
        /// </summary>
        private Task<IActionResult?> ValidateCustomerAccessAsync(int? workOrderCustomerId, int userId)
        {
            if (workOrderCustomerId != userId)
            {
                return Task.FromResult<IActionResult?>(StatusCode(403, new ApiResponse<object>(403, "Forbidden", "You can only view part usage for your own work orders.")));
            }
            return Task.FromResult<IActionResult?>(null);
        }

        /// <summary>
        /// Helper method to check center restriction for Staff/Technician
        /// </summary>
        private async Task<IActionResult?> ValidateCenterAccessAsync(int? partCenterId, string? userRole, int currentUserId)
        {
            if (userRole != UserRole.Staff.ToString() && userRole != UserRole.Technician.ToString())
                return null; // Not Staff/Technician, no restriction

            var currentUser = await _userDao.GetUserByIdAsync(currentUserId);
            if (currentUser == null)
                return NotFound(new ApiResponse<object>(404, "NotFound", "User not found."));

            var currentEmployee = await _employeeDao.GetEmployeeByIdAsync(currentUserId);
            if (currentEmployee == null)
                return BadRequest(new ApiResponse<object>(400, "BadRequest",
                    $"{currentUser.Role} user does not have an associated employee record."));

            if (partCenterId != currentEmployee.CenterId)
                return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                    $"{currentUser.Role} can only access part usage from their own service center (Center ID: {currentEmployee.CenterId})."));

            return null;
        }

        /// <summary>
        /// Helper method to get current user and employee for center validation
        /// </summary>
        private async Task<(User? User, Employee? Employee, IActionResult? Error)> GetCurrentUserAndEmployeeAsync(int currentUserId)
        {
            var currentUser = await _userDao.GetUserByIdAsync(currentUserId);
            if (currentUser == null)
                return (null, null, NotFound(new ApiResponse<object>(404, "NotFound", "User not found.")));

            var currentEmployee = await _employeeDao.GetEmployeeByIdAsync(currentUserId);
            if (currentEmployee == null)
                return (null, null, BadRequest(new ApiResponse<object>(400, "BadRequest",
                    $"{currentUser.Role} user does not have an associated employee record.")));

            return (currentUser, currentEmployee, null);
        }

        private PartUsageResponseDto MapToResponseDto(PartUsage partUsage)
        {
            decimal totalCost = partUsage.UnitCostPrice * partUsage.QuantityUsed;
            decimal totalPrice = partUsage.UnitPrice * partUsage.QuantityUsed;
            decimal profit = totalPrice - totalCost;
            decimal profitMargin = totalPrice > 0
                ? Math.Round(profit / totalPrice * 100, 2)
                : 0;

            return new PartUsageResponseDto
            {
                UsageId = partUsage.UsageId,
                HistoryId = partUsage.HistoryId,
                PartId = partUsage.PartId,
                QuantityUsed = partUsage.QuantityUsed,
                UnitCostPrice = partUsage.UnitCostPrice,
                UnitPrice = partUsage.UnitPrice,
                PartName = partUsage.Part?.PartName,
                PartDescription = partUsage.Part?.Description,
                TotalCost = totalCost,
                TotalPrice = totalPrice,
                Profit = profit,
                ProfitMargin = profitMargin,
                WorkOrderId = partUsage.History?.WorkOrderId,
                VehicleId = partUsage.History?.VehicleId
            };
        }
    }
}

