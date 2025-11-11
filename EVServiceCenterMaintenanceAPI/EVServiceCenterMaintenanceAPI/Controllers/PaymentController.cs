using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Helpers;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Services;
using EVServiceCenterMaintenanceAPI.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Net.payOS.Types;
using System.Security.Claims;
using System.Text.Json;
using static EVServiceCenterMaintenanceAPI.Enums.HostBookingUrl;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly EvserviceCenterDbContext _context;
        private readonly PayOSService _payOSService;
        private readonly EmailService _emailService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            EvserviceCenterDbContext context,
            PayOSService payOSService,
            EmailService emailService,
            ILogger<PaymentController> logger)
        {
            _context = context;
            _payOSService = payOSService;
            _emailService = emailService;
            _logger = logger;
        }

        private static DateTime GetVietNamTime(DateTime utcTime)
        {
            return utcTime.ConvertToVietnamTime();
        }

        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> PayOSWebhook([FromBody] WebhookType webhookBody)
        {
            try
            {
                _logger.LogInformation("Received PayOS webhook: {Data}", JsonSerializer.Serialize(webhookBody));

                try
                {
                    _payOSService.VerifyPaymentWebhookData(webhookBody);
                    _logger.LogInformation("Webhook signature verified successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Invalid webhook signature: {Error}", ex.Message);
                    return BadRequest(new { error = "Invalid signature" });
                }

                if (webhookBody.code != "00")
                {
                    _logger.LogWarning("Payment webhook received with non-success code: {Code}", webhookBody.code);
                    return Ok(new { message = "Acknowledged non-success payment" });
                }

                var webhookData = webhookBody.data;
                long orderCode = webhookData.orderCode;

                if (orderCode == 123 && webhookData.amount == 3000 && webhookData.description == "VQRIO123")
                {
                    _logger.LogInformation("Received PayOS test webhook for URL verification - returning OK");
                    return Ok(new
                    {
                        success = true,
                        message = "Test webhook received successfully",
                        note = "This is a test webhook from PayOS for URL verification",
                        orderCode = orderCode,
                        timestamp = DateTime.UtcNow
                    });
                }

                _logger.LogInformation("Processing payment for OrderCode {OrderCode}", orderCode);

                var workOrder = await _context.WorkOrders
                    .Include(w => w.Invoice)
                        .ThenInclude(i => i!.Payments)
                    .Include(w => w.Appointment)
                        .ThenInclude(a => a!.Customer)
                    .Include(w => w.Appointment)
                        .ThenInclude(a => a!.Slot)
                    .Include(w => w.Customer)
                    .Include(w => w.Vehicle)
                    .FirstOrDefaultAsync(w => w.OrderCode == orderCode.ToString());

                if (workOrder == null)
                {
                    _logger.LogWarning("WorkOrder not found for OrderCode: {OrderCode}", orderCode);
                    return NotFound(new { error = "WorkOrder not found" });
                }

                var existingPayment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.TransactionId == webhookData.reference);

                if (existingPayment != null)
                {
                    _logger.LogInformation("Payment already processed for transaction: {TransactionId}", webhookData.reference);
                    return Ok(new { message = "Payment already processed" });
                }

                DateTime paymentDateTime = DateTime.Parse(webhookData.transactionDateTime);
                if (paymentDateTime.Kind == DateTimeKind.Unspecified)
                {
                    paymentDateTime = DateTime.SpecifyKind(paymentDateTime, DateTimeKind.Utc);
                }
                else if (paymentDateTime.Kind == DateTimeKind.Local)
                {
                    paymentDateTime = paymentDateTime.ToUniversalTime();
                }

                Payment payment;
                string paymentType;

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    if (workOrder.Invoice == null)
                    {
                        paymentType = "Deposit";

                        if (workOrder.Appointment == null)
                        {
                            _logger.LogWarning("Deposit payment received but no appointment found for WorkOrder {WorkOrderId}", workOrder.WorkOrderId);
                            await transaction.RollbackAsync();
                            return BadRequest(new { error = "Deposit payment requires an appointment" });
                        }

                        if (workOrder.Appointment.Slot == null)
                        {
                            _logger.LogWarning("Appointment {AppointmentId} has no slot", workOrder.Appointment.AppointmentId);
                            await transaction.RollbackAsync();
                            return BadRequest(new { error = "Appointment slot not found" });
                        }

                        payment = new Payment
                        {
                            InvoiceId = null,
                            Amount = webhookData.amount,
                            Method = "PayOS",
                            PaymentType = paymentType,
                            OrderCode = orderCode.ToString(),
                            TransactionId = webhookData.reference,
                            PaymentDate = paymentDateTime,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.Payments.Add(payment);

                        // Check if slot is still available 
                        if (!workOrder.Appointment.Slot.IsAvailable)
                        {
                            _logger.LogWarning("Slot {SlotId} is no longer available for Appointment {AppointmentId}. Payment received but appointment cannot be confirmed.",
                                workOrder.Appointment.Slot.SlotId, workOrder.Appointment.AppointmentId);

                            // Mark appointment as cancelled due to slot unavailable
                            workOrder.Appointment.Status = AppointmentStatus.Cancelled.ToString();

                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();

                            return Ok(new
                            {
                                message = "Payment received but slot is no longer available. Please contact support for refund.",
                                appointmentId = workOrder.Appointment.AppointmentId,
                                paymentId = payment.PaymentId,
                                status = "SlotUnavailable",
                                requiresRefund = true
                            });
                        }

                        // Mark slot as unavailable after payment success
                        workOrder.Appointment.Slot.IsAvailable = false;
                        _logger.LogInformation("Marked Slot {SlotId} as unavailable after payment confirmation", workOrder.Appointment.Slot.SlotId);

                        // Set status to Confirmed after slot is verified and marked unavailable
                        workOrder.Appointment.Status = AppointmentStatus.Confirmed.ToString();
                        _logger.LogInformation("Updated Appointment {AppointmentId} status to Confirmed", workOrder.Appointment.AppointmentId);

                        // Save all changes (payment + appointment status + slot availability) in transaction
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation("Deposit payment processed successfully for WorkOrder {WorkOrderId}", workOrder.WorkOrderId);

                        // Send confirmation email using FireAndForget (after transaction commit)
                        if (!string.IsNullOrEmpty(workOrder.Customer?.Email))
                        {
                            // Get vehicle info
                            string vehicleInfo = workOrder.Vehicle != null
                                ? $"{workOrder.Vehicle.Model ?? "N/A"} ({workOrder.Vehicle.Plate ?? "N/A"})"
                                : "N/A";

                            string appointmentDate = GetVietNamTime(workOrder.Appointment.AppointmentDate).ToString("dd/MM/yyyy HH:mm");
                            string paymentDateFormatted = paymentDateTime.ConvertToVietnamTime().ToString("dd/MM/yyyy HH:mm:ss");

                            // Get customer name and email
                            string customerName = workOrder.Customer?.FullName ?? "Khách hàng";
                            string customerEmail = workOrder.Customer!.Email;
                            int workOrderIdCopy = workOrder.WorkOrderId;
                            decimal amountCopy = webhookData.amount;
                            string transactionId = webhookData.reference;
                            string orderCodeStr = orderCode.ToString();

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
                                    _logger.LogInformation("Deposit confirmation email sent to {Email}", customerEmail);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError("Failed to send deposit confirmation email: {Error}", ex.Message);
                                }
                            });
                        }
                    }
                    else
                    {
                        paymentType = "Final";

                        payment = new Payment
                        {
                            InvoiceId = workOrder.Invoice.InvoiceId,
                            Amount = webhookData.amount,
                            Method = "PayOS",
                            PaymentType = paymentType,
                            OrderCode = orderCode.ToString(),
                            TransactionId = webhookData.reference,
                            PaymentDate = paymentDateTime,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.Payments.Add(payment);

                        // Check if invoice is fully paid
                        decimal totalPaid = workOrder.Invoice.Payments.Sum(p => p.Amount) + payment.Amount;
                        if (totalPaid >= workOrder.Invoice.TotalAmount)
                        {
                            workOrder.Invoice.Status = "Paid";
                            _logger.LogInformation("Invoice {InvoiceId} is now fully paid", workOrder.Invoice.InvoiceId);
                        }
                        else
                        {
                            workOrder.Invoice.Status = "PartiallyPaid";
                            _logger.LogInformation("Invoice {InvoiceId} is partially paid: {Paid}/{Total}",
                                workOrder.Invoice.InvoiceId, totalPaid, workOrder.Invoice.TotalAmount);
                        }

                        // Save all changes (payment + invoice status) in transaction
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // Send final payment confirmation email using FireAndForget (after transaction commit)
                        if (!string.IsNullOrEmpty(workOrder.Customer?.Email))
                        {
                            // Get vehicle info
                            string vehicleInfo = workOrder.Vehicle != null
                                ? $"{workOrder.Vehicle.Model ?? "N/A"} ({workOrder.Vehicle.Plate ?? "N/A"})"
                                : "N/A";

                            string paymentDateFormatted = paymentDateTime.ConvertToVietnamTime().ToString("dd/MM/yyyy HH:mm:ss");

                            // Get customer name and email
                            string customerName = workOrder.Customer?.FullName ?? "Khách hàng";
                            string customerEmail = workOrder.Customer!.Email;

                            // Get invoice status (after commit, status is already updated)
                            string invoiceStatus = workOrder.Invoice.Status;

                            int invoiceIdCopy = workOrder.Invoice.InvoiceId;
                            int workOrderIdCopy = workOrder.WorkOrderId;
                            decimal amountCopy = webhookData.amount;
                            decimal totalAmountCopy = workOrder.Invoice.TotalAmount;
                            decimal totalPaidCopy = totalPaid;
                            string transactionId = webhookData.reference;
                            string orderCodeStr = orderCode.ToString();

                            TaskHelper.FireAndForget(async () =>
                            {
                                try
                                {
                                    await _emailService.SendFinalPaymentConfirmationEmailAsync(
                                        customerName,
                                        customerEmail,
                                        invoiceIdCopy,
                                        workOrderIdCopy,
                                        amountCopy,
                                        totalAmountCopy,
                                        totalPaidCopy,
                                        vehicleInfo,
                                        paymentDateFormatted,
                                        transactionId,
                                        orderCodeStr,
                                        invoiceStatus
                                    );
                                    _logger.LogInformation("Final payment confirmation email sent to {Email}", customerEmail);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError("Failed to send final confirmation email: {Error}", ex.Message);
                                }
                            });
                        }

                        _logger.LogInformation("Final payment processed successfully for Invoice {InvoiceId}", workOrder.Invoice.InvoiceId);
                    }

                    // Return success response (payment.PaymentId is available after SaveChangesAsync)
                    return Ok(new
                    {
                        message = "Payment processed successfully",
                        paymentId = payment.PaymentId,
                        paymentType = paymentType,
                        amount = payment.Amount
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error processing payment in transaction, rolling back");
                    throw; // Re-throw to be caught by outer catch block
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing PayOS webhook");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("workorder/{workOrderId}")]
        [Authorize]
        public async Task<IActionResult> GetWorkOrderPayments(int workOrderId)
        {
            try
            {
                // Verify WorkOrder exists
                var workOrder = await _context.WorkOrders.FindAsync(workOrderId);
                if (workOrder == null)
                {
                    return NotFound(new ApiResponse<object>(404, "NotFound", "WorkOrder not found"));
                }

                // Get all payments for this WorkOrder (includes deposits before invoice created and payments after invoice created)
                var payments = await _context.Payments
                    .Where(p => p.WorkOrderId == workOrderId)
                    .OrderBy(p => p.CreatedAt)
                    .Select(p => new PaymentResponseDto
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
                    })
                    .ToListAsync();

                var totalPaid = payments.Sum(p => p.Amount);

                return Ok(new ApiResponse<object>(200, "Success", "Payments retrieved successfully", data: new
                {
                    payments = payments,
                    totalPaid = totalPaid,
                    count = payments.Count
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments for WorkOrder {WorkOrderId}", workOrderId);
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("invoice/{invoiceId}")]
        [Authorize]
        public async Task<IActionResult> GetInvoicePayments(int invoiceId)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Payments)
                    .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

                if (invoice == null)
                {
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Invoice not found"));
                }

                var payments = invoice.Payments.Select(p => new PaymentResponseDto
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
                }).ToList();

                var totalPaid = payments.Sum(p => p.Amount);
                var remainingAmount = invoice.TotalAmount - totalPaid;

                return Ok(new ApiResponse<object>(200, "Success", "Payments retrieved successfully", data: new
                {
                    payments = payments.OrderBy(p => p.CreatedAt),
                    totalAmount = invoice.TotalAmount,
                    totalPaid = totalPaid,
                    remainingAmount = remainingAmount,
                    invoiceStatus = invoice.Status
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments for Invoice {InvoiceId}", invoiceId);
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpPost("create-final-link")]
        [Authorize]
        public async Task<IActionResult> CreateFinalPaymentLink([FromBody] FinalPaymentLinkRequestDto dto)
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

                // Get invoice with related data including services and parts
                var invoice = await _context.Invoices
                    .Include(i => i.WorkOrder)
                        .ThenInclude(w => w.Customer)
                    .Include(i => i.WorkOrder)
                        .ThenInclude(w => w.Vehicle)
                    .Include(i => i.WorkOrder)
                        .ThenInclude(w => w.AppointmentServices)
                            .ThenInclude(aps => aps.Service)
                    .Include(i => i.WorkOrder)
                        .ThenInclude(w => w.MaintenanceHistories)
                            .ThenInclude(mh => mh.PartUsages)
                                .ThenInclude(pu => pu.Part)
                    .Include(i => i.Payments)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(i => i.InvoiceId == dto.InvoiceId);

                if (invoice == null)
                {
                    return NotFound(new ApiResponse<object>(404, "NotFound", $"Invoice with ID {dto.InvoiceId} not found."));
                }

                var userIdClaim = User.FindFirst("UserId")?.Value;
                var userRole = User.FindFirstValue(ClaimTypes.Role);

                if (userRole == UserRole.Customer.ToString() && int.TryParse(userIdClaim, out int userId))
                {
                    if (invoice.WorkOrder?.CustomerId != userId)
                    {
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden", "You can only create payment link for your own invoices."));
                    }
                }

                // Validate invoice status using enum
                if (invoice.Status == InvoiceStatus.Cancelled.ToString())
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Cannot create payment link for cancelled invoice."));
                }

                if (invoice.Status == InvoiceStatus.Paid.ToString())
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Invoice is already fully paid."));
                }

                // Calculate remaining amount
                decimal totalPaid = invoice.Payments.Sum(p => p.Amount);
                decimal remainingAmount = invoice.TotalAmount - totalPaid;

                // If remaining amount is 0 or negative (due to rounding), mark invoice as paid and send email
                if (remainingAmount <= 0)
                {
                    // Update invoice status to Paid if not already
                    if (invoice.Status != InvoiceStatus.Paid.ToString())
                    {
                        invoice.Status = InvoiceStatus.Paid.ToString();
                        invoice.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Invoice {InvoiceId} marked as Paid (remainingAmount: {RemainingAmount})",
                            invoice.InvoiceId, remainingAmount);

                        // Send final payment confirmation email (same logic as webhook)
                        if (!string.IsNullOrEmpty(invoice.WorkOrder?.Customer?.Email))
                        {
                            // Get vehicle info
                            string vehicleInfo = invoice.WorkOrder.Vehicle != null
                                ? $"{invoice.WorkOrder.Vehicle.Model ?? "N/A"} ({invoice.WorkOrder.Vehicle.Plate ?? "N/A"})"
                                : "N/A";

                            // Format payment date - Use current time
                            string paymentDateFormatted = DateTime.UtcNow.ConvertToVietnamTime().ToString("dd/MM/yyyy HH:mm:ss");

                            // Get customer name and email
                            string customerName = invoice.WorkOrder.Customer?.FullName ?? "Khách hàng";
                            string customerEmail = invoice.WorkOrder.Customer!.Email;

                            // Get invoice status
                            string invoiceStatus = invoice.Status;

                            int invoiceIdCopy = invoice.InvoiceId;
                            int workOrderIdCopy = invoice.WorkOrderId;
                            decimal amountCopy = 0; // No additional payment needed
                            decimal totalAmountCopy = invoice.TotalAmount;
                            decimal totalPaidCopy = totalPaid;
                            string transactionId = "N/A"; // No transaction for zero amount
                            string orderCodeStr = "N/A"; // No order code for zero amount

                            TaskHelper.FireAndForget(async () =>
                            {
                                try
                                {
                                    await _emailService.SendFinalPaymentConfirmationEmailAsync(
                                        customerName,
                                        customerEmail,
                                        invoiceIdCopy,
                                        workOrderIdCopy,
                                        amountCopy,
                                        totalAmountCopy,
                                        totalPaidCopy,
                                        vehicleInfo,
                                        paymentDateFormatted,
                                        transactionId,
                                        orderCodeStr,
                                        invoiceStatus
                                    );
                                    _logger.LogInformation("Final payment confirmation email sent to {Email} for Invoice {InvoiceId}",
                                        customerEmail, invoiceIdCopy);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Failed to send final confirmation email for Invoice {InvoiceId}", invoiceIdCopy);
                                }
                            });
                        }
                    }

                    return Ok(new ApiResponse<object>(200, "Success",
                        "Invoice is already fully paid. No payment link needed. Invoice status updated and confirmation email sent."));
                }

                // Determine payment type and what is being paid
                bool hasDeposit = invoice.Payments.Any(p => p.PaymentType == PaymentType.Deposit.ToString());
                string paymentType = hasDeposit ? PaymentType.Final.ToString() : PaymentType.Full.ToString();

                // Build item list based on what customer is paying for
                var items = new List<ItemData>();
                string paymentDescription;

                if (hasDeposit)
                {
                    paymentDescription = $"Final payment for Invoice #{invoice.InvoiceId} - Parts";

                    var partUsages = invoice.WorkOrder?.MaintenanceHistories
                        .SelectMany(mh => mh.PartUsages)
                        .ToList() ?? new List<PartUsage>();

                    foreach (var pu in partUsages)
                    {
                        items.Add(new ItemData(
                            name: $"{pu.Part?.PartName ?? "Part"} (x{pu.QuantityUsed})",
                            quantity: pu.QuantityUsed,
                            price: (int)pu.UnitPrice
                        ));
                    }

                    _logger.LogInformation("Final payment (Parts only) for online booking - WorkOrder {WorkOrderId}, Remaining: {Amount} VND, Parts: {PartCount}",
                        invoice.WorkOrderId, remainingAmount, partUsages.Count);
                }
                else
                {
                    // Walk-in: No deposit paid yet
                    // Full payment = Services + Parts
                    paymentDescription = $"Full payment for Invoice #{invoice.InvoiceId} - Services & Parts";

                    // Add service items (walk-in always has services)
                    var services = invoice.WorkOrder?.AppointmentServices?.ToList() ?? new List<AppointmentService>();
                    foreach (var aps in services)
                    {
                        items.Add(new ItemData(
                            name: aps.Service?.ServiceName ?? "Service",
                            quantity: 1,
                            price: (int)aps.Price
                        ));
                    }

                    // Add part items (if any)
                    var partUsages = invoice.WorkOrder?.MaintenanceHistories
                        .SelectMany(mh => mh.PartUsages)
                        .ToList() ?? new List<PartUsage>();

                    foreach (var pu in partUsages)
                    {
                        items.Add(new ItemData(
                            name: $"{pu.Part?.PartName ?? "Part"} (x{pu.QuantityUsed})",
                            quantity: pu.QuantityUsed,
                            price: (int)pu.UnitPrice
                        ));
                    }

                    _logger.LogInformation("Full payment for walk-in - WorkOrder {WorkOrderId}, Amount: {Amount} VND, Services: {ServiceCount}, Parts: {PartCount}",
                        invoice.WorkOrderId, remainingAmount, services.Count, partUsages.Count);
                }

                // Fallback: Ensure items list is never empty (PayOS requirement)
                if (items.Count == 0)
                {
                    _logger.LogWarning("Unexpected: No items found for Invoice {InvoiceId}. Adding generic placeholder with remaining amount {Amount}",
                        invoice.InvoiceId, remainingAmount);
                    items.Add(new ItemData(
                        name: $"Payment for Invoice #{invoice.InvoiceId}",
                        quantity: 1,
                        price: (int)remainingAmount
                    ));
                }

                // Get URLs using static methods from HostBookingUrl
                string cancelUrl = GetCancelUrl(HostEnvironment.Local, invoice.WorkOrderId);
                string returnUrl = GetSuccessUrl(HostEnvironment.Local, invoice.WorkOrderId);

                // Create PayOS payment link (orderCode generated internally by service)
                var createPaymentResult = await _payOSService.CreatePaymentLink(
                    invoice.WorkOrderId,
                    remainingAmount,
                    paymentDescription,
                    items,
                    cancelUrl,
                    returnUrl
                );

                if (createPaymentResult == null)
                {
                    _logger.LogError("Failed to create PayOS payment link for Invoice {InvoiceId}", dto.InvoiceId);
                    return StatusCode(500, new ApiResponse<object>(500, "Error", "Failed to create payment link."));
                }

                // Generate QR code URL
                string qrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=300x300&data={Uri.EscapeDataString(createPaymentResult.checkoutUrl)}";

                var response = new PaymentLinkResponseDto
                {
                    CheckoutUrl = createPaymentResult.checkoutUrl,
                    QrCode = qrCodeUrl,
                    OrderCode = createPaymentResult.orderCode,
                    Amount = remainingAmount,
                    Description = paymentDescription,
                    InvoiceId = invoice.InvoiceId,
                    WorkOrderId = invoice.WorkOrderId,
                    PaymentType = paymentType,
                    CustomerName = invoice.WorkOrder?.Customer?.FullName,
                    CustomerEmail = invoice.WorkOrder?.Customer?.Email
                };

                _logger.LogInformation("{Role} created {PaymentType} payment link for Invoice {InvoiceId}, Amount: {Amount} VND, OrderCode: {OrderCode}",
                    userRole, paymentType, invoice.InvoiceId, remainingAmount, createPaymentResult.orderCode);

                return Ok(new ApiResponse<PaymentLinkResponseDto>(200, "Success", "Payment link created successfully.", null, response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating final payment link for Invoice {InvoiceId}", dto.InvoiceId);
                return StatusCode(500, new ApiResponse<object>(500, "InternalServerError", "An error occurred while creating payment link."));
            }
        }
    }
}

