using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Helpers;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
using EVServiceCenterMaintenanceAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentSlotController : ControllerBase
    {
        private readonly AppointmentSlotDao _appointmentSlotDao;
        private readonly ServiceCenterDao _serviceCenterDao;
        private readonly UserDao _userDao;
        private readonly EmployeeDao _employeeDao;
        private readonly AppointmentSlotGeneratorService _slotGenerator;
        private readonly ILogger<AppointmentSlotController> _logger;

        public AppointmentSlotController(
            AppointmentSlotDao appointmentSlotDao,
            ServiceCenterDao serviceCenterDao,
            UserDao userDao,
            EmployeeDao employeeDao,
            AppointmentSlotGeneratorService slotGenerator,
            ILogger<AppointmentSlotController> logger)
        {
            _appointmentSlotDao = appointmentSlotDao;
            _serviceCenterDao = serviceCenterDao;
            _userDao = userDao;
            _employeeDao = employeeDao;
            _slotGenerator = slotGenerator;
            _logger = logger;
        }

        private async Task<(bool Success, User? User, IActionResult? ErrorResponse)> GetCurrentUserAsync()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                return (false, null, Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID.")));

            var currentUser = await _userDao.GetUserByIdAsync(currentUserId);
            if (currentUser == null)
                return (false, null, NotFound(new ApiResponse<object>(404, "NotFound", "User not found.")));

            return (true, currentUser, null);
        }

        private async Task<(bool Success, Employee? Employee, IActionResult? ErrorResponse)> ValidateStaffOrTechnicianAccessAsync(User user, int targetCenterId, string action = "access")
        {
            if (user.Role != UserRole.Staff.ToString() && user.Role != UserRole.Technician.ToString())
                return (true, null, null); // Admin or other roles - allow access

            var employee = await _employeeDao.GetEmployeeByIdAsync(user.UserId);
            if (employee == null)
                return (false, null, BadRequest(new ApiResponse<object>(400, "BadRequest", $"{user.Role} user does not have an associated employee record.")));

            if (targetCenterId != employee.CenterId)
                return (false, null, StatusCode(403, new ApiResponse<object>(403, "Forbidden", $"{user.Role} can only {action} resources from their own service center (Center ID: {employee.CenterId}).")));

            return (true, employee, null);
        }

        private async Task<IActionResult?> ValidateCenterExistsAsync(int centerId)
        {
            var exists = await _serviceCenterDao.IsExistServiceCenterAsync(centerId);
            if (!exists)
                return NotFound(new ApiResponse<object>(404, "NotFound", $"Service Center with ID {centerId} not found."));
            return null;
        }

        private static AppointmentSlotResponseDto MapToResponseDto(AppointmentSlot slot)
        {
            return new AppointmentSlotResponseDto
            {
                SlotId = slot.SlotId,
                CenterId = slot.CenterId,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime,
                IsAvailable = slot.IsAvailable,
                CreatedAt = slot.CreatedAt,
                UpdatedAt = slot.UpdatedAt
            };
        }

        [HttpPost]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> CreateAppointmentSlot([FromBody] AppointmentSlotCreateRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(kvp => !string.IsNullOrEmpty(kvp.Key) && kvp.Key != "id" && kvp.Value?.Errors?.Count > 0)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? []);
                return BadRequest(new ApiResponse<object>(400, "Validation Error", "One or more validation errors occurred.", errors));
            }

            try
            {
                // After ModelState validation, these are guaranteed to be not null
                var centerId = dto.CenterId!.Value;
                var startTime = dto.StartTime!.Value;
                var endTime = dto.EndTime!.Value;

                // Validate CenterId exists
                var centerError = await ValidateCenterExistsAsync(centerId);
                if (centerError != null) return centerError;

                // Validate StartTime < EndTime
                if (startTime >= endTime)
                    return BadRequest(new ApiResponse<object>(400, "ValidationError", "StartTime must be earlier than EndTime."));

                // Validate StartTime is not in the past
                var nowVietnam = TimeZoneHelper.NowInVietnam;
                if (startTime <= nowVietnam)
                    return BadRequest(new ApiResponse<object>(400, "ValidationError", $"Cannot create slot in the past. StartTime must be after current time ({nowVietnam:dd/MM/yyyy HH:mm})."));

                // Validate slot time is within working hours (7:00 - 19:00)
                var slotStartTime = startTime.TimeOfDay;
                var slotEndTime = endTime.TimeOfDay;
                if (slotStartTime < SlotTimeConfig.StartTimeOfDay || slotEndTime > SlotTimeConfig.EndTimeOfDay)
                    return BadRequest(new ApiResponse<object>(400, "ValidationError", $"Slot time must be between {SlotTimeConfig.StartTimeOfDay:hh\\:mm} and {SlotTimeConfig.EndTimeOfDay:hh\\:mm}."));

                // Get current user and validate authorization
                var (success, currentUser, errorResponse) = await GetCurrentUserAsync();
                if (!success) return errorResponse!;

                // Staff can only create slots for their own center
                var (authSuccess, _, authError) = await ValidateStaffOrTechnicianAccessAsync(currentUser!, centerId, "create slots for");
                if (!authSuccess) return authError!;

                var slot = new AppointmentSlot
                {
                    CenterId = centerId,
                    StartTime = startTime,
                    EndTime = endTime,
                    IsAvailable = dto.IsAvailable ?? true
                };

                var createdSlot = await _appointmentSlotDao.CreateAppointmentSlotAsync(slot);
                var createdDto = MapToResponseDto(createdSlot);

                return CreatedAtAction(nameof(GetAppointmentSlot), new { id = createdSlot.SlotId }, new ApiResponse<AppointmentSlotResponseDto>(201, "Created", "Appointment slot created successfully.", data: createdDto));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<object>(400, "BadRequest", ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Staff,Technician,Admin")]
        public async Task<IActionResult> GetAppointmentSlot(int id)
        {
            try
            {
                var slot = await _appointmentSlotDao.GetAppointmentSlotByIdAsync(id);
                if (slot == null)
                    return NotFound(new ApiResponse<AppointmentSlotResponseDto>(404, "NotFound", "Appointment slot not found."));

                // Get current user and validate authorization
                var (success, currentUser, errorResponse) = await GetCurrentUserAsync();
                if (!success) return errorResponse!;

                // Staff and Technician can only view slots at their own center
                var (authSuccess, _, authError) = await ValidateStaffOrTechnicianAccessAsync(currentUser!, slot.CenterId, "view slots from");
                if (!authSuccess) return authError!;

                var dto = MapToResponseDto(slot);
                return Ok(new ApiResponse<AppointmentSlotResponseDto>(200, "Success", "Appointment slot retrieved successfully.", data: dto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet]
        [Authorize(Roles = "Staff,Technician,Admin")]
        public async Task<IActionResult> GetAllAppointmentSlots([FromQuery] AppointmentSlotQueryParams queryParams)
        {
            try
            {
                // Get current user and validate authorization
                var (success, currentUser, errorResponse) = await GetCurrentUserAsync();
                if (!success) return errorResponse!;

                // Validate CenterId if provided
                if (queryParams.CenterId.HasValue)
                {
                    var centerError = await ValidateCenterExistsAsync(queryParams.CenterId.Value);
                    if (centerError != null) return centerError;
                }

                // Staff and Technician can only view slots at their own center
                if (currentUser!.Role == UserRole.Staff.ToString() || currentUser.Role == UserRole.Technician.ToString())
                {
                    var employee = await _employeeDao.GetEmployeeByIdAsync(currentUser.UserId);
                    if (employee == null)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", $"{currentUser.Role} user does not have an associated employee record."));

                    // If CenterId in query params is different from their center -> Forbidden
                    if (queryParams.CenterId.HasValue && queryParams.CenterId.Value != employee.CenterId)
                    {
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden", $"{currentUser.Role} can only view slots from their own service center (Center ID: {employee.CenterId})."));
                    }

                    // Force filter by their center
                    queryParams.CenterId = employee.CenterId;
                }

                var (slots, total) = await _appointmentSlotDao.GetAllAppointmentSlotsAsync(queryParams);

                var dtos = slots.Select(MapToResponseDto).ToList();
                var responseData = new { slots = dtos, total, page = queryParams.Page, pageSize = queryParams.PageSize };
                return Ok(new ApiResponse<object>(200, "Success", "Appointment slots retrieved successfully.", data: responseData));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<object>(400, "BadRequest", ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("available")]
        public async Task<IActionResult> GetAvailableSlots([FromQuery] int centerId, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try
            {
                // Validate CenterId exists
                var centerError = await ValidateCenterExistsAsync(centerId);
                if (centerError != null) return centerError;
                var today = TimeZoneHelper.TodayInVietnam;
                var finalStartDate = startDate ?? today;
                var finalEndDate = endDate ?? finalStartDate.AddDays(6);

                var slots = await _appointmentSlotDao.GetAvailableSlotsAsync(centerId, finalStartDate, finalEndDate);
                var dtos = slots.Select(MapToResponseDto).ToList();

                return Ok(new ApiResponse<List<AppointmentSlotResponseDto>>(200, "Success", "Available slots retrieved successfully.", data: dtos));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<object>(400, "BadRequest", ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("available/day")]
        public async Task<IActionResult> GetAvailableSlotsForDay([FromQuery] int centerId, [FromQuery] DateTime date)
        {
            try
            {
                // Validate CenterId exists
                var centerError = await ValidateCenterExistsAsync(centerId);
                if (centerError != null) return centerError;

                var requestDate = date.Date;
                var todayVietnam = TimeZoneHelper.TodayInVietnam;

                if (requestDate < todayVietnam)
                {
                    return BadRequest(new ApiResponse<object>(
                        400, "BadRequest", $"Cannot retrieve slots for past date: {requestDate:dd/MM/yyyy}"));
                }

                var slots = await _appointmentSlotDao.GetAvailableSlotsAsync(centerId, requestDate, requestDate);
                var dtos = slots.Select(MapToResponseDto).ToList();

                return Ok(new ApiResponse<List<AppointmentSlotResponseDto>>(
                    200,
                    "Success",
                    $"Successfully retrieved {dtos.Count} slot(s) for date {requestDate:dd/MM/yyyy}.",
                    data: dtos
                ));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<object>(400, "BadRequest", ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(
                    500, "Error", ex.Message));
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> UpdateAppointmentSlot(int id, [FromBody] AppointmentSlotUpdateRequestDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new ApiResponse<object>(400, "BadRequest", "Empty request (DTO null)."));
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(kvp => !string.IsNullOrEmpty(kvp.Key) && kvp.Key != "id" && kvp.Value?.Errors?.Count > 0)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? []);
                return BadRequest(new ApiResponse<object>(400, "Validation Error", "One or more validation errors occurred.", errors));
            }

            try
            {
                // Get existing slot WITH Appointment relationship for validation
                var existingSlot = await _appointmentSlotDao.GetAppointmentSlotWithAppointmentAsync(id);
                if (existingSlot == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Appointment slot not found."));

                // Get current user and validate authorization
                var (success, currentUser, errorResponse) = await GetCurrentUserAsync();
                if (!success) return errorResponse!;

                // Staff can only update slots for their own center
                var (authSuccess, _, authError) = await ValidateStaffOrTechnicianAccessAsync(currentUser!, existingSlot.CenterId, "update slots from");
                if (!authSuccess) return authError!;

                // Only update IsAvailable field
                if (dto.IsAvailable.HasValue)
                {
                    existingSlot.IsAvailable = dto.IsAvailable.Value;
                }

                var updatedSlot = await _appointmentSlotDao.UpdateAppointmentSlotAsync(existingSlot);
                var updatedDto = MapToResponseDto(updatedSlot);

                return Ok(new ApiResponse<AppointmentSlotResponseDto>(200, "Success", "Appointment slot updated successfully.", data: updatedDto));
            }
            catch (InvalidOperationException ex)
            {
                // Cannot update IsAvailable or CenterId due to existing Appointment
                return StatusCode(409, new ApiResponse<object>(409, "Conflict", ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object>(404, "NotFound", ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<object>(400, "BadRequest", ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAppointmentSlot(int id)
        {
            try
            {
                var success = await _appointmentSlotDao.DeleteAppointmentSlotAsync(id);
                if (!success)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Appointment slot not found."));

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                // Slot has appointment or is already booked - cannot delete
                return StatusCode(409, new ApiResponse<object>(409, "Conflict", ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<object>(400, "BadRequest", ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpPost("generate/day")]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> GenerateSlotsForDay([FromQuery] int centerId, [FromQuery] string date)
        {
            try
            {
                // Validate CenterId exists
                var centerError = await ValidateCenterExistsAsync(centerId);
                if (centerError != null) return centerError;

                // Parse date
                if (!DateTime.TryParse(date, out var targetDate))
                {
                    return BadRequest(new ApiResponse<string>(
                        400,
                        "Bad Request",
                        "Định dạng ngày không hợp lệ. Vui lòng dùng format: yyyy-MM-dd (ví dụ: 2024-01-15)"
                    ));
                }

                // Validate date is not Sunday
                if (targetDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    return BadRequest(new ApiResponse<string>(
                        400,
                        "Bad Request",
                        $"Không thể tạo slot cho ngày Chủ nhật ({targetDate:dd/MM/yyyy}). Chỉ có thể tạo slot từ Thứ 2 đến Thứ 7."
                    ));
                }

                // Get current user and validate authorization
                var (success, currentUser, errorResponse) = await GetCurrentUserAsync();
                if (!success) return errorResponse!;

                // Staff can only create slots for their own center
                var (authSuccess, _, authError) = await ValidateStaffOrTechnicianAccessAsync(currentUser!, centerId, "create slots for");
                if (!authSuccess) return authError!;

                _logger.LogInformation("User {userId} đang tạo slot cho trung tâm {centerId} ngày {date}...",
                    currentUser!.UserId, centerId, targetDate.ToString("yyyy-MM-dd"));

                await _slotGenerator.GenerateSlotsForDateAndCenterAsync(targetDate, centerId);

                return Ok(new ApiResponse<string>(
                    200,
                    "Success",
                    $"Đã tạo slot thành công cho trung tâm {centerId} ngày {targetDate:yyyy-MM-dd} ({targetDate.DayOfWeek})",
                    null,
                    "Kiểm tra logs để xem chi tiết số lượng slot đã tạo"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo slot cho trung tâm {centerId} ngày {date}", centerId, date);
                return StatusCode(500, new ApiResponse<string>(
                    500,
                    "Error",
                    $"Lỗi khi tạo slot: {ex.Message}"
                ));
            }
        }

        [HttpPost("generate/week")]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> GenerateSlotsForWeek([FromQuery] int centerId, [FromQuery] string date)
        {
            try
            {
                // Validate CenterId exists
                var centerError = await ValidateCenterExistsAsync(centerId);
                if (centerError != null) return centerError;

                // Parse date
                if (!DateTime.TryParse(date, out var startDate))
                {
                    return BadRequest(new ApiResponse<string>(
                        400,
                        "Bad Request",
                        "Định dạng ngày không hợp lệ. Vui lòng dùng format: yyyy-MM-dd (ví dụ: 2025-01-15)"
                    ));
                }

                var startDateOnly = startDate.Date;
                var todayVietnam = TimeZoneHelper.TodayInVietnam;

                // Validate start date is not in the past
                if (startDateOnly < todayVietnam)
                {
                    return BadRequest(new ApiResponse<string>(
                        400,
                        "Bad Request",
                        $"Không thể tạo slot cho ngày trong quá khứ. Ngày bắt đầu phải từ hôm nay trở đi ({todayVietnam:dd/MM/yyyy})"
                    ));
                }

                // Get current user and validate authorization
                var (success, currentUser, errorResponse) = await GetCurrentUserAsync();
                if (!success) return errorResponse!;

                // Staff can only create slots for their own center
                var (authSuccess, _, authError) = await ValidateStaffOrTechnicianAccessAsync(currentUser!, centerId, "create slots for");
                if (!authSuccess) return authError!;

                _logger.LogInformation("User {userId} đang tạo slot cho trung tâm {centerId} bắt đầu từ ngày {date}...",
                    currentUser!.UserId, centerId, startDateOnly.ToString("yyyy-MM-dd"));

                var slotsCreated = await _slotGenerator.GenerateSlotsForWeekForCenterAsync(startDateOnly, centerId);

                if (slotsCreated == 0)
                {
                    return Ok(new ApiResponse<string>(
                        200,
                        "Success",
                        $"Không có slot mới nào được tạo cho trung tâm {centerId} bắt đầu từ ngày {startDateOnly:yyyy-MM-dd}. Tất cả các ngày (trừ Chủ nhật) đã có slot.",
                        null,
                        "Kiểm tra logs để xem chi tiết"
                    ));
                }

                return Ok(new ApiResponse<string>(
                    200,
                    "Success",
                    $"Đã tạo thành công {slotsCreated} slot cho trung tâm {centerId} bắt đầu từ ngày {startDateOnly:yyyy-MM-dd} (7 ngày, bỏ qua Chủ nhật)",
                    null,
                    "Kiểm tra logs để xem chi tiết số lượng slot đã tạo"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo slot cho trung tâm {centerId} từ ngày {date}", centerId, date);
                return StatusCode(500, new ApiResponse<string>(
                    500,
                    "Error",
                    $"Lỗi khi tạo slot: {ex.Message}"
                ));
            }
        }
    }
}
