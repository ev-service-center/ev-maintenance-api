using System.ComponentModel.DataAnnotations;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class PartUsageResponseDto
    {
        public int UsageId { get; set; }
        public int HistoryId { get; set; }
        public int PartId { get; set; }
        public int QuantityUsed { get; set; }
        public decimal UnitCostPrice { get; set; }
        public decimal UnitPrice { get; set; }

        // Related entity info
        public string? PartName { get; set; }
        public string? PartDescription { get; set; }

        // Financial calculations
        public decimal TotalCost { get; set; }        // QuantityUsed * UnitCostPrice
        public decimal TotalPrice { get; set; }       // QuantityUsed * UnitPrice
        public decimal Profit { get; set; }           // TotalPrice - TotalCost (VND)
        public decimal ProfitMargin { get; set; }     // (Profit / TotalPrice) * 100 (%)

        // Maintenance History info
        public int? WorkOrderId { get; set; }
        public int? VehicleId { get; set; }
    }

    public class PartUsageCreateRequestDto
    {
        [Required(ErrorMessage = "HistoryId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "HistoryId must be a positive number")]
        public int HistoryId { get; set; }

        [Required(ErrorMessage = "PartId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "PartId must be a positive number")]
        public int PartId { get; set; }

        [Required(ErrorMessage = "QuantityUsed is required")]
        [Range(1, 10000, ErrorMessage = "QuantityUsed must be between 1 and 10000")]
        public int QuantityUsed { get; set; }

        // Optional: Allow manual override of prices (default will be taken from Part)
        [Range(0, double.MaxValue, ErrorMessage = "UnitCostPrice must be non-negative")]
        public decimal? UnitCostPrice { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "UnitPrice must be non-negative")]
        public decimal? UnitPrice { get; set; }
    }

    public class PartUsageUpdateRequestDto
    {

        [Range(1, 10000, ErrorMessage = "QuantityUsed must be between 1 and 10000")]
        public int? QuantityUsed { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "UnitCostPrice must be non-negative")]
        public decimal? UnitCostPrice { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "UnitPrice must be non-negative")]
        public decimal? UnitPrice { get; set; }
    }

    public class PartUsageSummaryDto
    {
        public int HistoryId { get; set; }
        public int TotalPartsUsed { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal TotalProfit { get; set; }
        public List<PartUsageResponseDto> PartUsages { get; set; } = new List<PartUsageResponseDto>();
    }

    public class PartStockCheckDto
    {
        public int PartId { get; set; }
        public string PartName { get; set; } = null!;
        public int RequestedQuantity { get; set; }
        public int? CurrentStock { get; set; }
        public bool IsAvailable { get; set; }
        public int? MinStock { get; set; }
        public bool WillBeLowStock { get; set; }  // After usage, stock < MinStock
        public string? Message { get; set; }
    }
}

