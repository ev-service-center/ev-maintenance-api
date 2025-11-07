namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class AiSuggestionDto
    {
        
        public class PartAiUsageHistoryDto
        {
            public int PartId { get; set; }
            public string PartName { get; set; } = null!;
            public int QuantityUsed { get; set; }
            public DateTime Date { get; set; }
            public decimal Mileage { get; set; }
        }

        public class PartAiSuggestionDto
        {
            public int PartId { get; set; }
            public string PartName { get; set; } = null!;
            public string CurrentUsageTrend { get; set; } = null!;
            public int SuggestedMinStock { get; set; }
            public string Reason { get; set; } = null!;
        }
    }
}
