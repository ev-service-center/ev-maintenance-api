using System.Security.Claims;
using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Helpers;
using EVServiceCenterMaintenanceAPI.Models;
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

        public WorkOrderController(WorkOrderDao workOrderDao, EvserviceCenterDbContext context, PayOSService payOSService, ILogger<WorkOrderController> logger, EmailService emailService)
        {
            _workOrderDao = workOrderDao;
            _context = context;
            _payOSService = payOSService;
            _logger = logger;
            _emailService = emailService;
        }

        [HttpPost]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> CreateWorkOrder([FromBody] WorkOrderCreateRequestDto dto)
        {
            try
            {
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
                    ServiceDetails = workOrder.AppointmentServices?.Select(aps => new ServiceResponseDto
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

                        // Parse payment date and ensure UTC
                        DateTime paymentDateTime = DateTime.Parse(transaction.transactionDateTime);
                        if (paymentDateTime.Kind == DateTimeKind.Unspecified)
                        {
                            paymentDateTime = DateTime.SpecifyKind(paymentDateTime, DateTimeKind.Utc);
                        }

                        var payment = new Payment
                        {
                            WorkOrderId = workOrder.WorkOrderId,
                            InvoiceId = invoiceId,
                            Method = "PayOS",
                            Amount = paymentLinkInformation.amountPaid,
                            TransactionId = transaction.reference,
                            PaymentDate = paymentDateTime.ToUniversalTime(),
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

                            // Parse transaction time and convert to Vietnam time
                            DateTime transactionTime = DateTime.Parse(transaction.transactionDateTime);
                            if (transactionTime.Kind == DateTimeKind.Unspecified)
                            {
                                transactionTime = DateTime.SpecifyKind(transactionTime, DateTimeKind.Utc);
                            }
                            string paymentDateFormatted = transactionTime.ToUniversalTime().ConvertToVietnamTime().ToString("dd/MM/yyyy HH:mm:ss");

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
    }
}
