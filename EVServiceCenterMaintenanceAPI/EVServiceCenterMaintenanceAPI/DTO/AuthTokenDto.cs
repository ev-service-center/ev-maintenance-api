using System.ComponentModel.DataAnnotations;
using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class AuthTokenResponseDto
    {
        public int TokenId { get; set; }
        public int UserId { get; set; }
        public TokenType TokenType { get; set; }
        public string TokenValue { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class AuthTokenUpdateRequestDto
    {
        [Required] public int TokenId { get; set; }
        [Required] public int UserId { get; set; }
        [Required] public TokenType TokenType { get; set; }
        [Required] public string TokenValue { get; set; } = null!;
        [Required] public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
    }

    public class AuthTokenCreateRequestDto
    {
        [Required] public int UserId { get; set; }
        [Required] public TokenType TokenType { get; set; }
        [Required] public string TokenValue { get; set; } = null!;
        [Required] public DateTime ExpiresAt { get; set; }
    }
}
