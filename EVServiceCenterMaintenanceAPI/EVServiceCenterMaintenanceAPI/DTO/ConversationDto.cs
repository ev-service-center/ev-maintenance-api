using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class ConversationResponseDto
    {
        public int ConversationId { get; set; }
        public int CustomerId { get; set; }
        public int StaffId { get; set; }
        public ConversationStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
