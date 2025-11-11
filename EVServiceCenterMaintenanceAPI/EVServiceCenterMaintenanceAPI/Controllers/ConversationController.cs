using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConversationController : ControllerBase
    {
        private readonly ConversationDao _conversationDao;

        public ConversationController(ConversationDao conversationDao)
        {
            _conversationDao = conversationDao;
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Customer,Staff,Admin")]
        public async Task<IActionResult> GetConversation(int id)
        {
            try
            {
                var conversation = await _conversationDao.GetConversationByIdAsync(id);
                if (conversation == null)
                    return NotFound(new ApiResponse<ConversationResponseDto>(404, "NotFound", "Conversation not found."));

                var dto = new ConversationResponseDto
                {
                    ConversationId = conversation.ConversationId,
                    CustomerId = conversation.CustomerId,
                    StaffId = conversation.StaffId,
                    Status = Enum.Parse<ConversationStatus>(conversation.Status),
                    CreatedAt = conversation.CreatedAt,
                    UpdatedAt = conversation.UpdatedAt
                };

                return Ok(new ApiResponse<ConversationResponseDto>(200, "Success", "Conversation retrieved successfully.", data: dto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> GetAllConversations([FromQuery] ConversationQueryParams queryParams)
        {
            try
            {
                var (conversations, total) = await _conversationDao.GetAllConversationsAsync(queryParams);

                var dtos = conversations.Select(c => new ConversationResponseDto
                {
                    ConversationId = c.ConversationId,
                    CustomerId = c.CustomerId,
                    StaffId = c.StaffId,
                    Status = Enum.Parse<ConversationStatus>(c.Status),
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                }).ToList();

                var responseData = new { conversations = dtos, total, page = queryParams.Page, pageSize = queryParams.PageSize };
                return Ok(new ApiResponse<object>(200, "Success", "Conversations retrieved successfully.", data: responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpPost]
        [Authorize(Roles = "Customer,Staff")]
        public async Task<IActionResult> CreateConversation([FromBody] ConversationCreateRequestDto dto)
        {
            try
            {
                var conversation = new Conversation
                {
                    CustomerId = dto.CustomerId,
                    StaffId = dto.StaffId,
                    Status = Enums.ConversationStatus.Active.ToString()
                };

                var createdConversation = await _conversationDao.CreateConversationAsync(conversation);
                var createdDto = new ConversationResponseDto
                {
                    ConversationId = createdConversation.ConversationId,
                    CustomerId = createdConversation.CustomerId,
                    StaffId = createdConversation.StaffId,
                    Status = Enum.Parse<ConversationStatus>(createdConversation.Status),
                    CreatedAt = createdConversation.CreatedAt,
                    UpdatedAt = createdConversation.UpdatedAt
                };

                return CreatedAtAction(nameof(GetConversation), new { id = createdConversation.ConversationId }, new ApiResponse<ConversationResponseDto>(201, "Created", "Conversation created successfully.", data: createdDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("user/{userId}")]
        [Authorize(Roles = "Customer,Staff")]
        public async Task<IActionResult> GetConversationsByUser(int userId, [FromQuery] bool isCustomer = true)
        {
            try
            {
                var conversations = await _conversationDao.GetConversationsByUserIdAsync(userId, isCustomer);
                var dtos = conversations.Select(c => new ConversationResponseDto
                {
                    ConversationId = c.ConversationId,
                    CustomerId = c.CustomerId,
                    StaffId = c.StaffId,
                    Status = Enum.Parse<ConversationStatus>(c.Status),
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                }).ToList();

                return Ok(new ApiResponse<List<ConversationResponseDto>>(200, "Success", "Conversations retrieved successfully.", data: dtos));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> UpdateConversation(int id, [FromBody] ConversationUpdateRequestDto dto)
        {
            try
            {
                if (id != dto.ConversationId)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Conversation ID mismatch."));

                var conversation = new Conversation
                {
                    ConversationId = dto.ConversationId,
                    CustomerId = dto.CustomerId,
                    StaffId = dto.StaffId,
                    Status = dto.Status.ToString()
                };

                var updatedConversation = await _conversationDao.UpdateConversationAsync(conversation);
                var updatedDto = new ConversationResponseDto
                {
                    ConversationId = updatedConversation.ConversationId,
                    CustomerId = updatedConversation.CustomerId,
                    StaffId = updatedConversation.StaffId,
                    Status = Enum.Parse<ConversationStatus>(updatedConversation.Status),
                    CreatedAt = updatedConversation.CreatedAt,
                    UpdatedAt = updatedConversation.UpdatedAt
                };

                return Ok(new ApiResponse<ConversationResponseDto>(200, "Success", "Conversation updated successfully.", data: updatedDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConversation(int id)
        {
            try
            {
                var success = await _conversationDao.DeleteConversationAsync(id);
                if (!success)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Conversation not found."));

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
    }
}
