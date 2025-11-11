using EVServiceCenterMaintenanceAPI.Enums;
using System.ComponentModel.DataAnnotations;

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

    public class ConversationCreateRequestDto
    {
        [Required] public int CustomerId { get; set; }
        [Required] public int StaffId { get; set; }
    }
}
