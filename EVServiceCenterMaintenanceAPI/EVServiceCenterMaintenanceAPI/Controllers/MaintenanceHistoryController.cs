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

        [HttpGet]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> GetAllMaintenanceHistories([FromQuery] MaintenanceHistoryQueryParams queryParams)
        {
            try
            {
                var (histories, total) = await _maintenanceHistoryDao.GetAllMaintenanceHistoriesAsync(queryParams);

                var dtos = histories.Select(h => new MaintenanceHistoryResponseDto
                {
                    HistoryId = h.HistoryId,
                    VehicleId = h.VehicleId,
                    AppointmentId = h.AppointmentId,
                    MaintenanceDate = h.MaintenanceDate,
                    Description = h.Description,
                    Notes = h.Notes,
                    Cost = h.Cost,
                    MileageAtMaintenance = h.MileageAtMaintenance,
                    CreatedAt = h.CreatedAt,
                    UpdatedAt = h.UpdatedAt
                }).ToList();

                var responseData = new { histories = dtos, total, page = queryParams.Page, pageSize = queryParams.PageSize };
                return Ok(new ApiResponse<object>(200, "Success", "Maintenance histories retrieved successfully.", data: responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("vehicle/{vehicleId}")]
        [Authorize(Roles = "Customer,Staff,Technician,Admin")]
        public async Task<IActionResult> GetMaintenanceHistoriesByVehicle(int vehicleId)
        {
            try
            {
                var histories = await _maintenanceHistoryDao.GetMaintenanceHistoriesByVehicleIdAsync(vehicleId);
                var dtos = histories.Select(h => new MaintenanceHistoryResponseDto
                {
                    HistoryId = h.HistoryId,
                    VehicleId = h.VehicleId,
                    AppointmentId = h.AppointmentId,
                    MaintenanceDate = h.MaintenanceDate,
                    Description = h.Description,
                    Notes = h.Notes,
                    Cost = h.Cost,
                    MileageAtMaintenance = h.MileageAtMaintenance,
                    CreatedAt = h.CreatedAt,
                    UpdatedAt = h.UpdatedAt
                }).ToList();

                return Ok(new ApiResponse<List<MaintenanceHistoryResponseDto>>(200, "Success", "Maintenance histories retrieved successfully.", data: dtos));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Technician,Admin")]
        public async Task<IActionResult> UpdateMaintenanceHistory(int id, [FromBody] MaintenanceHistoryUpdateRequestDto dto)
        {
            try
            {
                if (id != dto.HistoryId)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Maintenance history ID mismatch."));

                var history = new MaintenanceHistory
                {
                    HistoryId = dto.HistoryId,
                    VehicleId = dto.VehicleId,
                    AppointmentId = dto.AppointmentId,
                    MaintenanceDate = dto.MaintenanceDate,
                    Description = dto.Description,
                    Notes = dto.Notes,
                    Cost = dto.Cost,
                    MileageAtMaintenance = dto.MileageAtMaintenance
                };

                var updatedHistory = await _maintenanceHistoryDao.UpdateMaintenanceHistoryAsync(history);
                var updatedDto = new MaintenanceHistoryResponseDto
                {
                    HistoryId = updatedHistory.HistoryId,
                    VehicleId = updatedHistory.VehicleId,
                    AppointmentId = updatedHistory.AppointmentId,
                    MaintenanceDate = updatedHistory.MaintenanceDate,
                    Description = updatedHistory.Description,
                    Notes = updatedHistory.Notes,
                    Cost = updatedHistory.Cost,
                    MileageAtMaintenance = updatedHistory.MileageAtMaintenance,
                    CreatedAt = updatedHistory.CreatedAt,
                    UpdatedAt = updatedHistory.UpdatedAt
                };

                return Ok(new ApiResponse<MaintenanceHistoryResponseDto>(200, "Success", "Maintenance history updated successfully.", data: updatedDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMaintenanceHistory(int id)
        {
            try
            {
                var success = await _maintenanceHistoryDao.DeleteMaintenanceHistoryAsync(id);
                if (!success)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Maintenance history not found."));

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
    }
}
