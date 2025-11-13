using System.ComponentModel.DataAnnotations;
using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class ChatResponseDto
    {
        public int ChatId { get; set; }
        public int ConversationId { get; set; }
        public int SenderId { get; set; }
        [Required] public string Message { get; set; } = null!;
        public DateTime SentDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ChatCreateRequestDto
    {
        [Required] public int ConversationId { get; set; }
        [Required] public int SenderId { get; set; }
        [Required] public string Message { get; set; } = null!;
    }

    public class ChatUpdateRequestDto
    {
        [Required] public int ChatId { get; set; }
        [Required] public int ConversationId { get; set; }
        [Required] public int SenderId { get; set; }
        [Required] public string Message { get; set; } = null!;
    }
}
