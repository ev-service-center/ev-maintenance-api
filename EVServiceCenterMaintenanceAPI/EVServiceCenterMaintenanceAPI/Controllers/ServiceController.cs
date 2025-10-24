using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using Microsoft.AspNetCore.Mvc;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceController : ControllerBase
    {
        private readonly ServiceDao _serviceDao;

        public ServiceController(ServiceDao serviceDao)
        {
            _serviceDao = serviceDao ?? throw new ArgumentNullException(nameof(serviceDao));
        }

        [HttpGet("active/{id}")]
        public async Task<IActionResult> GetActiveService(int id)
        {
            try
            {
                var service = await _serviceDao.GetActiveServiceByIdAsync(id);
                if (service == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Active service not found."));

                var dto = new ServiceResponseDto
                {
                    ServiceId = service.ServiceId,
                    ServiceName = service.ServiceName,
                    Description = service.Description,
                    BasePrice = service.BasePrice,
                    EstimatedTime = service.EstimatedTime,
                    Status = Enum.Parse<ServiceStatus>(service.Status),
                    ReminderIntervalDays = service.ReminderIntervalDays.Value,
                    ReminderMileage = service.ReminderMileage.Value,
                    Notes = service.Notes,
                    CreatedAt = service.CreatedAt,
                    UpdatedAt = service.UpdatedAt
                };

                return Ok(new ApiResponse<ServiceResponseDto>(200, "Success", "Active service retrieved successfully.", data: dto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to retrieve active service: {ex.Message}"));
            }
        }
    }
}
