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

        [HttpPost]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> CreateReminder([FromBody] ReminderCreateRequestDto dto)
        {
            try
            {
                var reminder = new Reminder
                {
                    UserId = dto.UserId,
                    VehicleId = dto.VehicleId,
                    ServiceId = dto.ServiceId,
                    ReminderType = dto.ReminderType.ToString(),
                    ReminderDate = dto.ReminderDate,
                    Message = dto.Message
                };

                var createdReminder = await _reminderDao.CreateReminderAsync(reminder);
                var createdDto = new ReminderResponseDto
                {
                    ReminderId = createdReminder.ReminderId,
                    UserId = createdReminder.UserId,
                    VehicleId = createdReminder.VehicleId,
                    ServiceId = createdReminder.ServiceId,
                    ReminderType = Enum.Parse<ReminderType>(createdReminder.ReminderType),
                    ReminderDate = createdReminder.ReminderDate,
                    Message = createdReminder.Message,
                    Sent = createdReminder.Sent.Value,
                    CreatedAt = createdReminder.CreatedAt,
                    UpdatedAt = createdReminder.UpdatedAt
                };

                return CreatedAtAction(nameof(GetReminder), new { id = createdReminder.ReminderId }, new ApiResponse<ReminderResponseDto>(201, "Created", "Reminder created successfully.", data: createdDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> GetAllReminders([FromQuery] ReminderQueryParams queryParams)
        {
            try
            {
                var (reminders, total) = await _reminderDao.GetAllRemindersAsync(queryParams);

                var dtos = reminders.Select(r => new ReminderResponseDto
                {
                    ReminderId = r.ReminderId,
                    UserId = r.UserId,
                    VehicleId = r.VehicleId,
                    ServiceId = r.ServiceId,
                    ReminderType = Enum.Parse<ReminderType>(r.ReminderType!),
                    ReminderDate = r.ReminderDate,
                    Message = r.Message,
                    Sent = r.Sent!.Value,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                }).ToList();

                var responseData = new { reminders = dtos, total, page = queryParams.Page, pageSize = queryParams.PageSize };
                return Ok(new ApiResponse<object>(200, "Success", "Reminders retrieved successfully.", data: responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
    }
}
