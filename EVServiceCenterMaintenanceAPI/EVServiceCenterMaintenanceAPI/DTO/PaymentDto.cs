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
}

