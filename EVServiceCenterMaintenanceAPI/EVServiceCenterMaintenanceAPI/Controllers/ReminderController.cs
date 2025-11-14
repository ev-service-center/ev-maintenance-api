using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReminderController : ControllerBase
    {
        private readonly ReminderDao _reminderDao;

        public ReminderController(ReminderDao reminderDao)
        {
            _reminderDao = reminderDao;
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Customer,Staff,Admin")]
        public async Task<IActionResult> GetReminder(int id)
        {
            try
            {
                var reminder = await _reminderDao.GetReminderByIdAsync(id);
                if (reminder == null)
                    return NotFound(new ApiResponse<ReminderResponseDto>(404, "NotFound", "Reminder not found."));

                var dto = new ReminderResponseDto
                {
                    ReminderId = reminder.ReminderId,
                    UserId = reminder.UserId,
                    VehicleId = reminder.VehicleId,
                    ServiceId = reminder.ServiceId,
                    ReminderType = Enum.Parse<ReminderType>(reminder.ReminderType!),
                    ReminderDate = reminder.ReminderDate,
                    Message = reminder.Message,
                    Sent = reminder.Sent!.Value,
                    CreatedAt = reminder.CreatedAt,
                    UpdatedAt = reminder.UpdatedAt
                };

                return Ok(new ApiResponse<ReminderResponseDto>(200, "Success", "Reminder retrieved successfully.", data: dto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
    }
}
