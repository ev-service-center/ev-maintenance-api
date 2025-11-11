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
    public class InvoiceController : ControllerBase
    {
        private readonly InvoiceDao _invoiceDao;
        private readonly EvserviceCenterDbContext _context;
        private readonly ILogger<InvoiceController> _logger;
        private readonly EmployeeDao _employeeDao;

        public InvoiceController(InvoiceDao invoiceDao, EvserviceCenterDbContext context, ILogger<InvoiceController> logger, EmployeeDao employeeDao)
        {
            _invoiceDao = invoiceDao;
            _context = context;
            _logger = logger;
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
        /// Validate Staff can only access invoices at their center
        /// </summary>
        private async Task<IActionResult?> ValidateCenterAccessAsync(int invoiceCenterId, string? userRole, int currentUserId)
        {
            if (userRole != UserRole.Staff.ToString())
                return null; // Not Staff, no restriction

            var currentEmployee = await _employeeDao.GetEmployeeByIdAsync(currentUserId);
            if (currentEmployee == null)
                return BadRequest(new ApiResponse<object>(400, "BadRequest",
                    $"{userRole} user does not have an associated employee record."));

            if (invoiceCenterId != currentEmployee.CenterId)
                return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                    $"{userRole} can only access invoices from their own service center (Center ID: {currentEmployee.CenterId})."));

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
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> CreateInvoice([FromBody] InvoiceCreateRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(kvp => kvp.Value?.Errors?.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>());
                    return BadRequest(new ApiResponse<object>(400, "ValidationError", "Invalid input data.", errors));
                }

                // Get current user info for authorization
                var (userIdClaim, userRole) = GetCurrentUserInfo();
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                // Validate WorkOrder exists
                var workOrder = await _context.WorkOrders
                    .Include(wo => wo.Center)
                    .FirstOrDefaultAsync(wo => wo.WorkOrderId == dto.WorkOrderId);
                if (workOrder == null)
                {
                    return NotFound(new ApiResponse<object>(404, "NotFound", $"WorkOrder with ID {dto.WorkOrderId} not found."));
                }

                // Center restriction: Staff chỉ tạo invoice tại center của họ
                if (userRole == UserRole.Staff.ToString())
                {
                    var centerAccessError = await ValidateCenterAccessAsync(workOrder.CenterId, userRole, currentUserId);
                    if (centerAccessError != null) return centerAccessError;
                }

                // Validate WorkOrder status
                if (workOrder.Status == WorkOrderStatus.Cancelled.ToString())
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        $"Cannot create invoice for cancelled WorkOrder {dto.WorkOrderId}."));
                }

                // Check if invoice already exists for this WorkOrder
                var existingInvoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.WorkOrderId == dto.WorkOrderId);
                if (existingInvoice != null)
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest",
                        $"Invoice already exists for WorkOrder {dto.WorkOrderId}. InvoiceId: {existingInvoice.InvoiceId}"));
                }

                // Auto-calculate total from Services + Parts
                decimal calculatedTotal = await _invoiceDao.CalculateWorkOrderTotalAsync(dto.WorkOrderId);

                // Validate calculated total is greater than 0
                if (calculatedTotal <= 0)
                {
                    return BadRequest(new ApiResponse<object>(400, "ValidationError",
                        "Cannot create invoice. WorkOrder must have at least one service or part with cost greater than 0."));
                }

                // Validate: If TotalAmount is provided, it must match calculated total exactly
                if (dto.TotalAmount > 0 && dto.TotalAmount != calculatedTotal)
                {
                    _logger.LogWarning("Invoice TotalAmount mismatch for WorkOrder {WorkOrderId}: Provided={Provided}, Calculated={Calculated}",
                        dto.WorkOrderId, dto.TotalAmount, calculatedTotal);
                    return BadRequest(new ApiResponse<object>(400, "ValidationError",
                        $"TotalAmount mismatch. Expected: {calculatedTotal:N0} VND, Provided: {dto.TotalAmount:N0} VND. " +
                        $"Please use auto-calculated amount or verify your calculation."));
                }

                // Validate DueDate is not in the past
                if (dto.DueDate.HasValue && dto.DueDate.Value.Date < DateTime.UtcNow.Date)
                {
                    return BadRequest(new ApiResponse<object>(400, "ValidationError",
                        "DueDate cannot be in the past."));
                }

                decimal finalTotal = calculatedTotal;

                var invoice = new Invoice
                {
                    WorkOrderId = dto.WorkOrderId,
                    TotalAmount = finalTotal,
                    DueDate = dto.DueDate ?? DateTime.UtcNow.AddDays(7), // Default: 7 days
                    Status = InvoiceStatus.Unpaid.ToString()
                };

                var createdInvoice = await _invoiceDao.CreateInvoiceAsync(invoice);

                // Link existing deposit payments to this invoice
                var depositPayments = await _context.Payments
                    .Where(p => p.WorkOrderId == dto.WorkOrderId && p.PaymentType == "Deposit" && p.InvoiceId == null)
                    .ToListAsync();

                decimal totalPaid = 0;
                if (depositPayments.Count != 0)
                {
                    foreach (var payment in depositPayments)
                    {
                        payment.InvoiceId = createdInvoice.InvoiceId;
                        payment.UpdatedAt = DateTime.UtcNow;
                        totalPaid += payment.Amount;
                    }
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Linked {Count} deposit payment(s) totaling {Amount} VND to Invoice {InvoiceId}",
                        depositPayments.Count, totalPaid, createdInvoice.InvoiceId);
                }

                // Update invoice status based on payments
                if (totalPaid > 0)
                {
                    if (totalPaid >= createdInvoice.TotalAmount)
                    {
                        createdInvoice.Status = InvoiceStatus.Paid.ToString();
                    }
                    else
                    {
                        createdInvoice.Status = InvoiceStatus.PartiallyPaid.ToString();
                    }
                    await _context.SaveChangesAsync();
                }

                var responseDto = new InvoiceResponseDto
                {
                    InvoiceId = createdInvoice.InvoiceId,
                    WorkOrderId = createdInvoice.WorkOrderId,
                    TotalAmount = createdInvoice.TotalAmount,
                    IssueDate = createdInvoice.IssueDate,
                    DueDate = createdInvoice.DueDate,
                    Status = Enum.Parse<InvoiceStatus>(createdInvoice.Status ?? "Unpaid"),
                    CreatedAt = createdInvoice.CreatedAt,
                    UpdatedAt = createdInvoice.UpdatedAt,
                    TotalPaid = totalPaid,
                    RemainingAmount = createdInvoice.TotalAmount - totalPaid
                };

                _logger.LogInformation("Invoice {InvoiceId} created for WorkOrder {WorkOrderId} with TotalAmount {TotalAmount}",
                    createdInvoice.InvoiceId, dto.WorkOrderId, finalTotal);

                return CreatedAtAction(nameof(GetInvoice), new { id = createdInvoice.InvoiceId },
                    new ApiResponse<InvoiceResponseDto>(201, "Created", "Invoice created successfully.", null, responseDto));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Failed to create invoice: {Error}", ex.Message);
                return BadRequest(new ApiResponse<object>(400, "BadRequest", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice for WorkOrder {WorkOrderId}", dto.WorkOrderId);
                return StatusCode(500, new ApiResponse<object>(500, "InternalServerError", "An error occurred while creating invoice."));
            }
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetInvoice(int id)
        {
            try
            {
                var invoice = await _invoiceDao.GetInvoiceByIdAsync(id);
                if (invoice == null)
                {
                    return NotFound(new ApiResponse<object>(404, "NotFound", $"Invoice with ID {id} not found."));
                }

                // Authorization: Customer chỉ xem invoice của mình
                var userIdClaim = User.FindFirst("UserId")?.Value;
                var userRole = User.FindFirstValue(ClaimTypes.Role);

                if (userRole == "Customer" && int.TryParse(userIdClaim, out int userId))
                {
                    if (invoice.WorkOrder?.CustomerId != userId)
                    {
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden", "You can only view your own invoices."));
                    }
                }

                // Calculate payment summary
                decimal totalPaid = invoice.Payments.Sum(p => p.Amount);
                decimal remaining = invoice.TotalAmount - totalPaid;

                var detailDto = new InvoiceDetailResponseDto
                {
                    InvoiceId = invoice.InvoiceId,
                    WorkOrderId = invoice.WorkOrderId,
                    TotalAmount = invoice.TotalAmount,
                    IssueDate = invoice.IssueDate,
                    DueDate = invoice.DueDate,
                    Status = Enum.Parse<InvoiceStatus>(invoice.Status ?? "Unpaid"),
                    CreatedAt = invoice.CreatedAt,
                    UpdatedAt = invoice.UpdatedAt,
                    WorkOrderStatus = invoice.WorkOrder?.Status,
                    CustomerName = invoice.WorkOrder?.Customer?.FullName,
                    CustomerEmail = invoice.WorkOrder?.Customer?.Email,
                    VehicleModel = invoice.WorkOrder?.Vehicle?.Model,
                    VehiclePlate = invoice.WorkOrder?.Vehicle?.Plate,
                    TotalPaid = totalPaid,
                    RemainingAmount = remaining,
                    Payments = invoice.Payments.Select(p => new PaymentResponseDto
                    {
                        PaymentId = p.PaymentId,
                        InvoiceId = p.InvoiceId,
                        PaymentDate = p.PaymentDate,
                        Amount = p.Amount,
                        Method = p.Method,
                        PaymentType = p.PaymentType,
                        TransactionId = p.TransactionId,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    }).ToList(),
                    Services = invoice.WorkOrder?.AppointmentServices?.Select(aps => new InvoiceServiceItemDto
                    {
                        ServiceId = aps.ServiceId,
                        ServiceName = aps.Service?.ServiceName ?? "N/A",
                        Price = aps.Price
                    }).ToList() ?? new List<InvoiceServiceItemDto>(),
                    Parts = invoice.WorkOrder?.MaintenanceHistories
                        .SelectMany(mh => mh.PartUsages)
                        .Select(pu => new InvoicePartItemDto
                        {
                            PartId = pu.PartId,
                            PartName = pu.Part?.PartName ?? "N/A",
                            QuantityUsed = pu.QuantityUsed,
                            UnitPrice = pu.UnitPrice,
                            TotalPrice = pu.QuantityUsed * pu.UnitPrice
                        }).ToList() ?? new List<InvoicePartItemDto>()
                };

                return Ok(new ApiResponse<InvoiceDetailResponseDto>(200, "Success", "Invoice retrieved successfully.", null, detailDto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoice {InvoiceId}", id);
                return StatusCode(500, new ApiResponse<object>(500, "InternalServerError", "An error occurred while retrieving invoice."));
            }
        }

        [HttpGet("workorder/{workOrderId}")]
        [Authorize]
        public async Task<IActionResult> GetInvoiceByWorkOrder(int workOrderId)
        {
            try
            {
                var invoice = await _invoiceDao.GetInvoiceByWorkOrderIdAsync(workOrderId);
                if (invoice == null)
                {
                    return NotFound(new ApiResponse<object>(404, "NotFound", $"No invoice found for WorkOrder {workOrderId}."));
                }

                // Authorization: Customer chỉ xem invoice của mình
                var userIdClaim = User.FindFirst("UserId")?.Value;
                var userRole = User.FindFirstValue(ClaimTypes.Role);

                if (userRole == "Customer" && int.TryParse(userIdClaim, out int userId))
                {
                    if (invoice.WorkOrder?.CustomerId != userId)
                    {
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden", "You can only view your own invoices."));
                    }
                }

                decimal totalPaid = invoice.Payments.Sum(p => p.Amount);
                decimal remaining = invoice.TotalAmount - totalPaid;

                var responseDto = new InvoiceResponseDto
                {
                    InvoiceId = invoice.InvoiceId,
                    WorkOrderId = invoice.WorkOrderId,
                    TotalAmount = invoice.TotalAmount,
                    IssueDate = invoice.IssueDate,
                    DueDate = invoice.DueDate,
                    Status = Enum.Parse<InvoiceStatus>(invoice.Status ?? "Unpaid"),
                    CreatedAt = invoice.CreatedAt,
                    UpdatedAt = invoice.UpdatedAt,
                    TotalPaid = totalPaid,
                    RemainingAmount = remaining,
                    Payments = invoice.Payments.Select(p => new PaymentResponseDto
                    {
                        PaymentId = p.PaymentId,
                        InvoiceId = p.InvoiceId,
                        PaymentDate = p.PaymentDate,
                        Amount = p.Amount,
                        Method = p.Method,
                        PaymentType = p.PaymentType,
                        TransactionId = p.TransactionId,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    }).ToList()
                };

                return Ok(new ApiResponse<InvoiceResponseDto>(200, "Success", "Invoice retrieved successfully.", null, responseDto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoice for WorkOrder {WorkOrderId}", workOrderId);
                return StatusCode(500, new ApiResponse<object>(500, "InternalServerError", "An error occurred while retrieving invoice."));
            }
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAllInvoices([FromQuery] InvoiceQueryParams queryParams)
        {
            try
            {
                // Authorization: Customer chỉ xem invoices của mình
                var (userIdClaim, userRole) = GetCurrentUserInfo();

                if (userRole == UserRole.Customer.ToString() && int.TryParse(userIdClaim, out int userId))
                {
                    // Force filter by CustomerId for customers
                    queryParams.CustomerId = userId;
                    _logger.LogInformation("Customer {CustomerId} retrieving their invoices", userId);
                }
                // Center restriction: Staff chỉ xem invoices tại center của họ
                else if (userRole == UserRole.Staff.ToString())
                {
                    if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                        return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                    var (employee, error) = await GetCurrentEmployeeAsync(currentUserId, userRole);
                    if (error != null) return error;

                    // Force filter by staff's center
                    queryParams.CenterId = employee!.CenterId;
                }
                // Admin can see all

                var (invoices, total) = await _invoiceDao.GetAllInvoicesAsync(queryParams);

                var dtos = invoices.Select(i =>
                {
                    decimal totalPaid = i.Payments.Sum(p => p.Amount);
                    return new InvoiceResponseDto
                    {
                        InvoiceId = i.InvoiceId,
                        WorkOrderId = i.WorkOrderId,
                        TotalAmount = i.TotalAmount,
                        IssueDate = i.IssueDate,
                        DueDate = i.DueDate,
                        Status = Enum.Parse<InvoiceStatus>(i.Status ?? "Unpaid"),
                        CreatedAt = i.CreatedAt,
                        UpdatedAt = i.UpdatedAt,
                        TotalPaid = totalPaid,
                        RemainingAmount = i.TotalAmount - totalPaid
                    };
                }).ToList();

                var result = new
                {
                    invoices = dtos,
                    total,
                    page = queryParams.Page,
                    pageSize = queryParams.PageSize,
                    totalPages = (int)Math.Ceiling(total / (double)queryParams.PageSize)
                };

                return Ok(new ApiResponse<object>(200, "Success", "Invoices retrieved successfully.", null, result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices");
                return StatusCode(500, new ApiResponse<object>(500, "InternalServerError", "An error occurred while retrieving invoices."));
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> UpdateInvoice(int id, [FromBody] InvoiceUpdateRequestDto dto)
        {
            try
            {
                var invoice = await _invoiceDao.GetInvoiceByIdAsync(id);
                if (invoice == null)
                {
                    return NotFound(new ApiResponse<object>(404, "NotFound", $"Invoice with ID {id} not found."));
                }

                // Calculate current total paid
                decimal totalPaid = invoice.Payments.Sum(p => p.Amount);

                // Validate TotalAmount update
                if (dto.TotalAmount.HasValue)
                {
                    if (dto.TotalAmount.Value < totalPaid)
                    {
                        return BadRequest(new ApiResponse<object>(400, "ValidationError",
                            $"Cannot update TotalAmount to {dto.TotalAmount.Value:N0} VND. " +
                            $"Invoice already has payments totaling {totalPaid:N0} VND."));
                    }
                    invoice.TotalAmount = dto.TotalAmount.Value;
                }

                // Validate DueDate update
                if (dto.DueDate.HasValue)
                {
                    if (dto.DueDate.Value.Date < DateTime.UtcNow.Date)
                    {
                        return BadRequest(new ApiResponse<object>(400, "ValidationError",
                            "DueDate cannot be in the past."));
                    }
                    invoice.DueDate = dto.DueDate.Value;
                }

                // Validate Status update
                if (dto.Status.HasValue)
                {
                    var currentStatus = Enum.Parse<InvoiceStatus>(invoice.Status ?? "Unpaid");
                    var newStatus = dto.Status.Value;

                    // Validate status transitions
                    if (currentStatus == InvoiceStatus.Paid && newStatus != InvoiceStatus.Paid && newStatus != InvoiceStatus.Cancelled)
                    {
                        return BadRequest(new ApiResponse<object>(400, "ValidationError",
                            "Cannot change status from Paid to " + newStatus + ". Only Cancelled is allowed."));
                    }

                    if (currentStatus == InvoiceStatus.Cancelled && newStatus != InvoiceStatus.Cancelled)
                    {
                        return BadRequest(new ApiResponse<object>(400, "ValidationError",
                            "Cannot change status from Cancelled to " + newStatus + "."));
                    }

                    // Auto-validate status matches payment status
                    if (newStatus == InvoiceStatus.Paid && totalPaid < invoice.TotalAmount)
                    {
                        return BadRequest(new ApiResponse<object>(400, "ValidationError",
                            $"Cannot set status to Paid. Invoice has only {totalPaid:N0} VND paid out of {invoice.TotalAmount:N0} VND."));
                    }

                    if (newStatus == InvoiceStatus.Unpaid && totalPaid > 0)
                    {
                        return BadRequest(new ApiResponse<object>(400, "ValidationError",
                            $"Cannot set status to Unpaid. Invoice already has payments totaling {totalPaid:N0} VND."));
                    }

                    invoice.Status = newStatus.ToString();
                }

                var updatedInvoice = await _invoiceDao.UpdateInvoiceAsync(invoice);

                // Reload invoice with payments to get accurate data
                updatedInvoice = await _invoiceDao.GetInvoiceByIdAsync(id)
                    ?? throw new InvalidOperationException($"Invoice {id} not found after update.");

                // Recalculate total paid after update
                decimal updatedTotalPaid = updatedInvoice.Payments.Sum(p => p.Amount);

                var responseDto = new InvoiceResponseDto
                {
                    InvoiceId = updatedInvoice.InvoiceId,
                    WorkOrderId = updatedInvoice.WorkOrderId,
                    TotalAmount = updatedInvoice.TotalAmount,
                    IssueDate = updatedInvoice.IssueDate,
                    DueDate = updatedInvoice.DueDate,
                    Status = Enum.Parse<InvoiceStatus>(updatedInvoice.Status ?? "Unpaid"),
                    CreatedAt = updatedInvoice.CreatedAt,
                    UpdatedAt = updatedInvoice.UpdatedAt,
                    TotalPaid = updatedTotalPaid,
                    RemainingAmount = updatedInvoice.TotalAmount - updatedTotalPaid
                };

                _logger.LogInformation("Invoice {InvoiceId} updated successfully", id);

                return Ok(new ApiResponse<InvoiceResponseDto>(200, "Success", "Invoice updated successfully.", null, responseDto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating invoice {InvoiceId}", id);
                return StatusCode(500, new ApiResponse<object>(500, "InternalServerError", "An error occurred while updating invoice."));
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteInvoice(int id)
        {
            try
            {
                await _invoiceDao.DeleteInvoiceAsync(id);
                _logger.LogInformation("Invoice {InvoiceId} deleted successfully", id);
                return Ok(new ApiResponse<object>(200, "Success", "Invoice deleted successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object>(404, "NotFound", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>(400, "BadRequest", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting invoice {InvoiceId}", id);
                return StatusCode(500, new ApiResponse<object>(500, "InternalServerError", "An error occurred while deleting invoice."));
            }
        }
    }
}

