using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
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

        [HttpGet]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> GetAllChats([FromQuery] ChatQueryParams queryParams)
        {
            try
            {
                var (chats, total) = await _chatDao.GetAllChatsAsync(queryParams);

                var dtos = chats.Select(c => new ChatResponseDto
                {
                    ChatId = c.ChatId,
                    ConversationId = c.ConversationId,
                    SenderId = c.SenderId,
                    Message = c.Message,
                    SentDate = c.SentDate,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                }).ToList();

                var responseData = new { chats = dtos, total, page = queryParams.Page, pageSize = queryParams.PageSize };
                return Ok(new ApiResponse<object>(200, "Success", "Chats retrieved successfully.", data: responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("conversation/{conversationId}")]
        [Authorize(Roles = "Customer,Staff")]
        public async Task<IActionResult> GetChatsByConversation(int conversationId)
        {
            try
            {
                var chats = await _chatDao.GetChatsByConversationIdAsync(conversationId);
                var dtos = chats.Select(c => new ChatResponseDto
                {
                    ChatId = c.ChatId,
                    ConversationId = c.ConversationId,
                    SenderId = c.SenderId,
                    Message = c.Message,
                    SentDate = c.SentDate,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                }).ToList();

                return Ok(new ApiResponse<List<ChatResponseDto>>(200, "Success", "Chats retrieved successfully.", data: dtos));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Customer,Staff")]
        public async Task<IActionResult> UpdateChat(int id, [FromBody] ChatUpdateRequestDto dto)
        {
            try
            {
                if (id != dto.ChatId)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Chat ID mismatch."));

                var chat = new Chat
                {
                    ChatId = dto.ChatId,
                    ConversationId = dto.ConversationId,
                    SenderId = dto.SenderId,
                    Message = dto.Message
                };

                var updatedChat = await _chatDao.UpdateChatAsync(chat);
                var updatedDto = new ChatResponseDto
                {
                    ChatId = updatedChat.ChatId,
                    ConversationId = updatedChat.ConversationId,
                    SenderId = updatedChat.SenderId,
                    Message = updatedChat.Message,
                    SentDate = updatedChat.SentDate,
                    CreatedAt = updatedChat.CreatedAt,
                    UpdatedAt = updatedChat.UpdatedAt
                };

                return Ok(new ApiResponse<ChatResponseDto>(200, "Success", "Chat updated successfully.", data: updatedDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteChat(int id)
        {
            try
            {
                var success = await _chatDao.DeleteChatAsync(id);
                if (!success)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Chat not found."));

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

    }
}
