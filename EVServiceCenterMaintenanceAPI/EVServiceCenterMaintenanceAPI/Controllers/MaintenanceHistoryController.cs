using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MaintenanceHistoryController : ControllerBase
    {
        private readonly MaintenanceHistoryDao _maintenanceHistoryDao;

        public MaintenanceHistoryController(MaintenanceHistoryDao maintenanceHistoryDao)
        {
            _maintenanceHistoryDao = maintenanceHistoryDao;
        }

        [HttpPost]
        [Authorize(Roles = "Technician,Admin")]
        public async Task<IActionResult> CreateMaintenanceHistory([FromBody] MaintenanceHistoryCreateRequestDto dto)
        {
            try
            {
                var history = new MaintenanceHistory
                {
                    VehicleId = dto.VehicleId,
                    AppointmentId = dto.AppointmentId,
                    MaintenanceDate = dto.MaintenanceDate,
                    Description = dto.Description,
                    Notes = dto.Notes,
                    Cost = dto.Cost,
                    MileageAtMaintenance = dto.MileageAtMaintenance
                };

                var createdHistory = await _maintenanceHistoryDao.CreateMaintenanceHistoryAsync(history);
                var createdDto = new MaintenanceHistoryResponseDto
                {
                    HistoryId = createdHistory.HistoryId,
                    VehicleId = createdHistory.VehicleId,
                    AppointmentId = createdHistory.AppointmentId,
                    MaintenanceDate = createdHistory.MaintenanceDate,
                    Description = createdHistory.Description,
                    Notes = createdHistory.Notes,
                    Cost = createdHistory.Cost,
                    MileageAtMaintenance = createdHistory.MileageAtMaintenance,
                    CreatedAt = createdHistory.CreatedAt,
                    UpdatedAt = createdHistory.UpdatedAt
                };

                return CreatedAtAction(nameof(GetMaintenanceHistory), new { id = createdHistory.HistoryId }, new ApiResponse<MaintenanceHistoryResponseDto>(201, "Created", "Maintenance history created successfully.", data: createdDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Customer,Staff,Technician,Admin")]
        public async Task<IActionResult> GetMaintenanceHistory(int id)
        {
            try
            {
                var history = await _maintenanceHistoryDao.GetMaintenanceHistoryByIdAsync(id);
                if (history == null)
                    return NotFound(new ApiResponse<MaintenanceHistoryResponseDto>(404, "NotFound", "Maintenance history not found."));

                var dto = new MaintenanceHistoryResponseDto
                {
                    HistoryId = history.HistoryId,
                    VehicleId = history.VehicleId,
                    AppointmentId = history.AppointmentId,
                    MaintenanceDate = history.MaintenanceDate,
                    Description = history.Description,
                    Notes = history.Notes,
                    Cost = history.Cost,
                    MileageAtMaintenance = history.MileageAtMaintenance,
                    CreatedAt = history.CreatedAt,
                    UpdatedAt = history.UpdatedAt
                };

                return Ok(new ApiResponse<MaintenanceHistoryResponseDto>(200, "Success", "Maintenance history retrieved successfully.", data: dto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
    }
}
