using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly AppointmentSlotGeneratorService _slotGenerator;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            AppointmentSlotGeneratorService slotGenerator,
            ILogger<AdminController> logger)
        {
            _slotGenerator = slotGenerator;
            _logger = logger;
        }

        [HttpPost("generate-slots")]
        public async Task<IActionResult> GenerateSlots()
        {
            try
            {
                _logger.LogInformation("Admin đang trigger tạo slot thủ công...");

                await _slotGenerator.GenerateSlotsForNext7DaysAsync();

                return Ok(new ApiResponse<string>(
                    200,
                    "Success",
                    "Đã tạo slot thành công cho 7 ngày tới (bỏ qua Chủ nhật)",
                    null,
                    "Kiểm tra logs để xem chi tiết số lượng slot đã tạo"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo slot thủ công");
                return StatusCode(500, new ApiResponse<string>(
                    500,
                    "Error",
                    $"Lỗi khi tạo slot: {ex.Message}"
                ));
            }
        }

        [HttpPost("generate-slots/{date}")]
        public async Task<IActionResult> GenerateSlotsForDate(string date)
        {
            try
            {
                if (!DateTime.TryParse(date, out var targetDate))
                {
                    return BadRequest(new ApiResponse<string>(
                        400,
                        "Bad Request",
                        "Định dạng ngày không hợp lệ. Vui lòng dùng format: yyyy-MM-dd (ví dụ: 2024-01-15)"
                    ));
                }

                _logger.LogInformation("Admin đang trigger tạo slot cho ngày {date}...", targetDate.ToString("yyyy-MM-dd"));

                await _slotGenerator.GenerateSlotsForSpecificDateAsync(targetDate);

                return Ok(new ApiResponse<string>(
                    200,
                    "Success",
                    $"Đã tạo slot thành công cho ngày {targetDate:yyyy-MM-dd} ({targetDate.DayOfWeek})",
                    null,
                    "Kiểm tra logs để xem chi tiết số lượng slot đã tạo"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo slot cho ngày {date}", date);
                return StatusCode(500, new ApiResponse<string>(
                    500,
                    "Error",
                    $"Lỗi khi tạo slot: {ex.Message}"
                ));
            }
        }
    }
}
