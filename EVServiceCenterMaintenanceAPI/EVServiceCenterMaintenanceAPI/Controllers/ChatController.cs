using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly ChatDao _chatDao;

        public ChatController(ChatDao chatDao)
        {
            _chatDao = chatDao;
        }

        [HttpPost]
        [Authorize(Roles = "Customer,Staff")]
        public async Task<IActionResult> CreateChat([FromBody] ChatCreateRequestDto dto)
        {
            try
            {
                var chat = new Chat
                {
                    ConversationId = dto.ConversationId,
                    SenderId = dto.SenderId,
                    Message = dto.Message
                };

                var createdChat = await _chatDao.CreateChatAsync(chat);
                var createdDto = new ChatResponseDto
                {
                    ChatId = createdChat.ChatId,
                    ConversationId = createdChat.ConversationId,
                    SenderId = createdChat.SenderId,
                    Message = createdChat.Message,
                    SentDate = createdChat.SentDate,
                    CreatedAt = createdChat.CreatedAt,
                    UpdatedAt = createdChat.UpdatedAt
                };

                return CreatedAtAction(nameof(GetChat), new { id = createdChat.ChatId }, new ApiResponse<ChatResponseDto>(201, "Created", "Chat created successfully.", data: createdDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Customer,Staff,Admin")]
        public async Task<IActionResult> GetChat(int id)
        {
            try
            {
                var chat = await _chatDao.GetChatByIdAsync(id);
                if (chat == null)
                    return NotFound(new ApiResponse<ChatResponseDto>(404, "NotFound", "Chat not found."));

                var dto = new ChatResponseDto
                {
                    ChatId = chat.ChatId,
                    ConversationId = chat.ConversationId,
                    SenderId = chat.SenderId,
                    Message = chat.Message,
                    SentDate = chat.SentDate,
                    CreatedAt = chat.CreatedAt,
                    UpdatedAt = chat.UpdatedAt
                };

                return Ok(new ApiResponse<ChatResponseDto>(200, "Success", "Chat retrieved successfully.", data: dto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
    }
}
