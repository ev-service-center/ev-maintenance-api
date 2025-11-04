using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
using EVServiceCenterMaintenanceAPI.Enums;

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


        [HttpGet("active/{id}")]
        public async Task<IActionResult> GetActiveServiceCenter(int id)
        {
            try
            {
                var center = await _serviceCenterDao.GetActiveServiceCenterByIdAsync(id);
                if (center == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Active service center not found."));

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

                return Ok(new ApiResponse<ServiceCenterResponseDto>(200, "Success", "Active service center retrieved successfully.", data: dto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to retrieve active service center: {ex.Message}"));
            }
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetAllActiveServiceCenters([FromQuery] ServiceCenterQueryParams queryParams)
        {
            try
            {
                var (centers, total) = await _serviceCenterDao.GetAllActiveServiceCentersAsync(queryParams);

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
                return Ok(new ApiResponse<object>(200, "Success", "Active service centers retrieved successfully.", data: responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to retrieve active service centers: {ex.Message}"));
            }
        }

       
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateServiceCenter(int id, [FromBody] ServiceCenterUpdateRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray());
                return BadRequest(new ApiResponse<object>(400, "Validation Error", "One or more validation errors occurred.", errors));
            }

            try
            {
                if (id != dto.CenterId)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Service center ID mismatch."));

                var center = new ServiceCenter
                {
                    CenterId = dto.CenterId,
                    CenterName = dto.CenterName,
                    Address = dto.Address,
                    Phone = dto.Phone,
                    Email = dto.Email,
                    Status = dto.Status.ToString()
                };

                var updatedCenter = await _serviceCenterDao.UpdateServiceCenterAsync(center);
                var updatedDto = new ServiceCenterResponseDto
                {
                    CenterId = updatedCenter.CenterId,
                    CenterName = updatedCenter.CenterName,
                    Address = updatedCenter.Address,
                    Phone = updatedCenter.Phone,
                    Email = updatedCenter.Email,
                    Status = Enum.Parse<ServiceCenterStatus>(updatedCenter.Status),
                    CreatedAt = updatedCenter.CreatedAt,
                    UpdatedAt = updatedCenter.UpdatedAt
                };

                return Ok(new ApiResponse<ServiceCenterResponseDto>(200, "Success", "Service center updated successfully.", data: updatedDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to update service center: {ex.Message}"));
            }
        }
    }
}