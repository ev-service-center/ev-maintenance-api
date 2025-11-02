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

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateServiceCenter([FromBody] ServiceCenterCreateRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
                return BadRequest(new ApiResponse<object>(400, "Validation Error", "One or more validation errors occurred.", errors));
            }

            try
            {
                var center = new ServiceCenter
                {
                    CenterName = dto.CenterName,
                    Address = dto.Address,
                    Phone = dto.Phone,
                    Email = dto.Email,
                    Status = dto.Status.ToString()
                };

                var createdCenter = await _serviceCenterDao.CreateServiceCenterAsync(center);
                var createdDto = new ServiceCenterResponseDto
                {
                    CenterId = createdCenter.CenterId,
                    CenterName = createdCenter.CenterName,
                    Address = createdCenter.Address,
                    Phone = createdCenter.Phone,
                    Email = createdCenter.Email,
                    Status = Enum.Parse<ServiceCenterStatus>(createdCenter.Status),
                    CreatedAt = createdCenter.CreatedAt,
                    UpdatedAt = createdCenter.UpdatedAt
                };

                return CreatedAtAction(nameof(GetServiceCenter), new { id = createdCenter.CenterId }, new ApiResponse<ServiceCenterResponseDto>(
                    201, "Created", "Service center created successfully.", data: createdDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to create service center: {ex.Message}"));
            }
        }

        [HttpGet]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> GetAllServiceCenters([FromQuery] ServiceCenterQueryParams queryParams)
        {
            try
            {
                var (centers, total) = await _serviceCenterDao.GetAllServiceCentersAsync(queryParams);

                var dtos = centers.Select(c => new ServiceCenterResponseDto
                {
                    CenterId = c.CenterId,
                    CenterName = c.CenterName,
                    Address = c.Address,
                    Phone = c.Phone,
                    Email = c.Email,
                    Status = Enum.Parse<ServiceCenterStatus>(c.Status),
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                }).ToList();

                var responseData = new { centers = dtos, total, page = queryParams.Page, pageSize = queryParams.PageSize };
                return Ok(new ApiResponse<object>(200, "Success", "Service centers retrieved successfully.", data: responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to retrieve service centers: {ex.Message}"));
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteServiceCenter(int id)
        {
            try
            {
                await _serviceCenterDao.DeleteServiceCenterAsync(id);
                return Ok(new ApiResponse<object>(200, "Success", "Service center deleted successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object>(404, "NotFound", ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to delete service center: {ex.Message}"));
            }
        }
    }
}
