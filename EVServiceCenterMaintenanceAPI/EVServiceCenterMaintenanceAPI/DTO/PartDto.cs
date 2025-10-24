namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class PartSuggestionDto
    {
        public int PartId { get; set; }
        public string PartName { get; set; } = null!;
        public int CurrentStock { get; set; }
        public int MinStock { get; set; }
        public int SuggestedOrderQuantity { get; set; }
    }
}
