using System.ComponentModel.DataAnnotations;
using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class InvoiceCreateRequestDto
    {
        [Required]
        public int WorkOrderId { get; set; }

        public decimal TotalAmount { get; set; }

        public DateTime? DueDate { get; set; }
    }

    public class InvoiceUpdateRequestDto
    {
        [Range(0.01, double.MaxValue, ErrorMessage = "TotalAmount must be greater than 0")]
        public decimal? TotalAmount { get; set; }

        public DateTime? DueDate { get; set; }

        public InvoiceStatus? Status { get; set; }
    }

    public class InvoiceResponseDto
    {
        public int InvoiceId { get; set; }
        public int WorkOrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? DueDate { get; set; }
        public InvoiceStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public List<PaymentResponseDto>? Payments { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal RemainingAmount { get; set; }
    }

    public class InvoiceDetailResponseDto
    {
        public int InvoiceId { get; set; }
        public int WorkOrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? DueDate { get; set; }
        public InvoiceStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // WorkOrder details
        public string? WorkOrderStatus { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? VehicleModel { get; set; }
        public string? VehiclePlate { get; set; }

        public List<PaymentResponseDto> Payments { get; set; } = new();
        public decimal TotalPaid { get; set; }
        public decimal RemainingAmount { get; set; }

        public List<InvoiceServiceItemDto> Services { get; set; } = new();

        public List<InvoicePartItemDto> Parts { get; set; } = new();
    }

    public class InvoiceServiceItemDto
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = null!;
        public decimal Price { get; set; }
    }

    public class InvoicePartItemDto
    {
        public int PartId { get; set; }
        public string PartName { get; set; } = null!;
        public int QuantityUsed { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}

