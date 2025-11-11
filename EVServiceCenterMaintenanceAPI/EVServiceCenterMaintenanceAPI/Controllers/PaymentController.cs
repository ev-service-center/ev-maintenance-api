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
using System.Text.Json;

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
                    .Include(w => w.Customer)
                    .Include(w => w.Vehicle)
                    .Include(w => w.Appointment)
                        .ThenInclude(a => a!.Slot)
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

                Payment payment;
                string paymentType;

                if (workOrder.Invoice == null)
                {
                    paymentType = "Deposit";

                    payment = new Payment
                    {
                        InvoiceId = null,
                        WorkOrderId = workOrder.WorkOrderId,
                        Amount = webhookData.amount,
                        Method = "PayOS",
                        PaymentType = paymentType,
                        OrderCode = orderCode.ToString(),
                        TransactionId = webhookData.reference,
                        PaymentDate = DateTime.Parse(webhookData.transactionDateTime),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Payments.Add(payment);

                    // Update Appointment status to Confirmed
                    if (workOrder.Appointment != null)
                    {
                        workOrder.Appointment.Status = AppointmentStatus.Confirmed.ToString();
                        _logger.LogInformation("Updated Appointment {AppointmentId} status to Confirmed", workOrder.Appointment.AppointmentId);
                    }

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Deposit payment processed successfully for WorkOrder {WorkOrderId}", workOrder.WorkOrderId);

                    // Send confirmation email using FireAndForget
                    if (!string.IsNullOrEmpty(workOrder.Customer?.Email))
                    {
                        // Get vehicle info
                        string vehicleInfo = workOrder.Vehicle != null
                            ? $"{workOrder.Vehicle.Model ?? "N/A"} ({workOrder.Vehicle.Plate ?? "N/A"})"
                            : "N/A";

                        // Get appointment date
                        string appointmentDate = workOrder.Appointment != null
                            ? GetVietNamTime(workOrder.Appointment.AppointmentDate).ToString("dd/MM/yyyy HH:mm")
                            : "N/A";

                        // Format payment date - Parse → Ensure UTC → VN time
                        DateTime transactionTime = DateTime.Parse(webhookData.transactionDateTime);
                        // If datetime doesn't have timezone info, assume UTC
                        if (transactionTime.Kind == DateTimeKind.Unspecified)
                        {
                            transactionTime = DateTime.SpecifyKind(transactionTime, DateTimeKind.Utc);
                        }
                        string paymentDateFormatted = transactionTime.ToUniversalTime().ConvertToVietnamTime().ToString("dd/MM/yyyy HH:mm:ss");

                        // Get customer name and email (email already validated above)
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

                    _logger.LogInformation("Deposit payment processed successfully for WorkOrder {WorkOrderId}", workOrder.WorkOrderId);
                }
                else
                {
                    paymentType = "Final";

                    // Parse payment date and ensure UTC
                    DateTime paymentDateTime = DateTime.Parse(webhookData.transactionDateTime);
                    if (paymentDateTime.Kind == DateTimeKind.Unspecified)
                    {
                        paymentDateTime = DateTime.SpecifyKind(paymentDateTime, DateTimeKind.Utc);
                    }

                    payment = new Payment
                    {
                        InvoiceId = workOrder.Invoice.InvoiceId,
                        WorkOrderId = workOrder.WorkOrderId,
                        Amount = webhookData.amount,
                        Method = "PayOS",
                        PaymentType = paymentType,
                        OrderCode = orderCode.ToString(),
                        TransactionId = webhookData.reference,
                        PaymentDate = paymentDateTime.ToUniversalTime(),
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

                    await _context.SaveChangesAsync();

                    // Send final payment confirmation email using FireAndForget
                    if (!string.IsNullOrEmpty(workOrder.Customer?.Email))
                    {
                        // Get vehicle info
                        string vehicleInfo = workOrder.Vehicle != null
                            ? $"{workOrder.Vehicle.Model ?? "N/A"} ({workOrder.Vehicle.Plate ?? "N/A"})"
                            : "N/A";

                        // Format payment date - Parse → Ensure UTC → VN time
                        DateTime finalTransactionTime = DateTime.Parse(webhookData.transactionDateTime);
                        if (finalTransactionTime.Kind == DateTimeKind.Unspecified)
                        {
                            finalTransactionTime = DateTime.SpecifyKind(finalTransactionTime, DateTimeKind.Utc);
                        }
                        string paymentDateFormatted = finalTransactionTime.ToUniversalTime().ConvertToVietnamTime().ToString("dd/MM/yyyy HH:mm:ss");

                        // Get customer name and email (email already validated above)
                        string customerName = workOrder.Customer?.FullName ?? "Khách hàng";
                        string customerEmail = workOrder.Customer!.Email;

                        // Get invoice status
                        string invoiceStatus = workOrder.Invoice?.Status ?? "Unknown";

                        int invoiceIdCopy = workOrder.Invoice?.InvoiceId ?? 0;
                        int workOrderIdCopy = workOrder.WorkOrderId;
                        decimal amountCopy = webhookData.amount;
                        decimal totalAmountCopy = workOrder.Invoice?.TotalAmount ?? 0;
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

                    _logger.LogInformation("Final payment processed successfully for Invoice {InvoiceId}", workOrder.Invoice?.InvoiceId ?? 0);
                }

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
                // Get payments linked to WorkOrder (deposits before invoice created)
                // Note: Với giải pháp OrderCode, không còn WorkOrderId trong Payment
                var workOrderPayments = new List<PaymentResponseDto>();

                // Query by OrderCode pattern
                var workOrder = await _context.WorkOrders.FindAsync(workOrderId);
                if (workOrder == null)
                {
                    return NotFound(new ApiResponse<object>(404, "NotFound", "WorkOrder not found"));
                }

                // Get payments linked to Invoice (if invoice exists)
                await _context.Entry(workOrder)
                    .Reference(w => w.Invoice)
                    .Query()
                    .Include(i => i.Payments)
                    .LoadAsync();

                if (workOrder.Invoice != null)
                {
                    var invoicePayments = workOrder.Invoice.Payments.Select(p => new PaymentResponseDto
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
                    });

                    workOrderPayments.AddRange(invoicePayments);
                }

                var totalPaid = workOrderPayments.Sum(p => p.Amount);

                return Ok(new ApiResponse<object>(200, "Success", "Payments retrieved successfully", data: new
                {
                    payments = workOrderPayments.OrderBy(p => p.CreatedAt),
                    totalPaid = totalPaid,
                    count = workOrderPayments.Count
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
    }
}

