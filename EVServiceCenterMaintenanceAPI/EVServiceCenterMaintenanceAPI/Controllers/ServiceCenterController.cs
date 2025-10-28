using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceCenterController : ControllerBase
    {
        private readonly ServiceCenterDao _serviceCenterDao;

        public ServiceCenterController(ServiceCenterDao serviceCenterDao)
        {
            _serviceCenterDao = serviceCenterDao ?? throw new ArgumentNullException(nameof(serviceCenterDao));
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Staff,Technician,Admin")]
        public async Task<IActionResult> GetServiceCenter(int id)
        {
            try
            {
                var center = await _serviceCenterDao.GetServiceCenterByIdAsync(id);
                if (center == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Service center not found."));

                var dto = new ServiceCenterResponseDto
                {
                    CenterId = center.CenterId,
                    CenterName = center.CenterName,
                    Address = center.Address,
                    Phone = center.Phone,
                    Email = center.Email,
                    Status = Enum.Parse<ServiceCenterStatus>(center.Status),
                    CreatedAt = center.CreatedAt,
                    UpdatedAt = center.UpdatedAt
                };

                return Ok(new ApiResponse<ServiceCenterResponseDto>(200, "Success", "Service center retrieved successfully.", data: dto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to retrieve service center: {ex.Message}"));
            }
        }
    }
}
