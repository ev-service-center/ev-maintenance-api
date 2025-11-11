using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
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
    }
}
