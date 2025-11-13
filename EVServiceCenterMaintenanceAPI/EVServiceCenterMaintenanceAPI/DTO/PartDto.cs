using System.ComponentModel.DataAnnotations;
using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class PartResponseDto
    {
        public int PartId { get; set; }
        [Required, StringLength(100)] public string PartName { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int QuantityInStock { get; set; }
        public int MinStock { get; set; }
        public int CenterId { get; set; }
        public PartStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PartAdminResponseDto : PartResponseDto
    {
        public decimal CostPrice { get; set; }
        public decimal Profit => Price - CostPrice;
        public decimal ProfitMargin => Price > 0 ? Math.Round((Price - CostPrice) / Price * 100, 2) : 0;
    }

    public class PartCreateRequestDto
    {
        [Required, StringLength(100)] public string PartName { get; set; } = null!;
        public string? Description { get; set; }
        [Required, Range(0, double.MaxValue)] public decimal CostPrice { get; set; }
        [Required, Range(0, double.MaxValue)] public decimal Price { get; set; }
        public int QuantityInStock { get; set; }
        public int MinStock { get; set; }
        [Required] public int CenterId { get; set; }
        public PartStatus Status { get; set; }
    }

    public class PartSuggestionDto
    {
        public int PartId { get; set; }
        public string PartName { get; set; } = null!;
        public int CurrentStock { get; set; }
        public int MinStock { get; set; }
        public int SuggestedOrderQuantity { get; set; }
    }

    public class PartUpdateRequestDto
    {
        [StringLength(100)] public string? PartName { get; set; }
        public string? Description { get; set; }
        [Range(0, double.MaxValue)] public decimal? CostPrice { get; set; }
        [Range(0, double.MaxValue)] public decimal? Price { get; set; }
        public int? QuantityInStock { get; set; }
        public int? MinStock { get; set; }
        public int? CenterId { get; set; }
        public PartStatus? Status { get; set; }
    }
}
