using System.ComponentModel.DataAnnotations;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class PaymentResponseDto
    {
        public int PaymentId { get; set; }
        public int? InvoiceId { get; set; }
        public DateTime? PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = null!;
        public string? PaymentType { get; set; }  // "Deposit", "Final", "Full"
        public string? TransactionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class FinalPaymentLinkRequestDto
    {
        [Required(ErrorMessage = "InvoiceId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "InvoiceId must be a positive number")]
        public int InvoiceId { get; set; }
    }

    public class PaymentLinkResponseDto
    {
        public string CheckoutUrl { get; set; } = null!;
        public string QrCode { get; set; } = null!;
        public long OrderCode { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = null!;
        public int InvoiceId { get; set; }
        public int WorkOrderId { get; set; }
        public string PaymentType { get; set; } = null!;
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
    }
}

