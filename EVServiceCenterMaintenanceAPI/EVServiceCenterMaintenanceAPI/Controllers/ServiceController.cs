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
    public class ServiceController : ControllerBase
    {
        private readonly ServiceDao _serviceDao;

        public ServiceController(ServiceDao serviceDao)
        {
            _serviceDao = serviceDao ?? throw new ArgumentNullException(nameof(serviceDao));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateService([FromBody] ServiceCreateRequestDto dto)
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
                var service = new Service
                {
                    ServiceName = dto.ServiceName,
                    Description = dto.Description,
                    BasePrice = dto.BasePrice,
                    EstimatedTime = dto.EstimatedTime,
                    Status = dto.Status.ToString(),
                    ReminderIntervalDays = dto.ReminderIntervalDays,
                    ReminderMileage = dto.ReminderMileage,
                    Notes = dto.Notes
                };

                var createdService = await _serviceDao.CreateServiceAsync(service);
                var createdDto = new ServiceResponseDto
                {
                    ServiceId = createdService.ServiceId,
                    ServiceName = createdService.ServiceName,
                    Description = createdService.Description,
                    BasePrice = createdService.BasePrice,
                    EstimatedTime = createdService.EstimatedTime,
                    Status = Enum.Parse<ServiceStatus>(createdService.Status),
                    ReminderIntervalDays = createdService.ReminderIntervalDays.Value,
                    ReminderMileage = createdService.ReminderMileage.Value,
                    Notes = createdService.Notes,
                    CreatedAt = createdService.CreatedAt,
                    UpdatedAt = createdService.UpdatedAt
                };

                return CreatedAtAction(nameof(GetService), new { id = createdService.ServiceId }, new ApiResponse<ServiceResponseDto>(
                    201, "Created", "Service created successfully.", data: createdDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to create service: {ex.Message}"));
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Customer,Staff,Technician,Admin")]
        public async Task<IActionResult> GetService(int id)
        {
            try
            {
                var service = await _serviceDao.GetServiceByIdAsync(id);
                if (service == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Service not found."));

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

                return Ok(new ApiResponse<ServiceResponseDto>(200, "Success", "Service retrieved successfully.", data: dto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to retrieve service: {ex.Message}"));
            }
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
                    ReminderIntervalDays = service.ReminderIntervalDays!.Value,
                    ReminderMileage = service.ReminderMileage!.Value,
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

        [HttpGet("active")]
        public async Task<IActionResult> GetAllActiveServices([FromQuery] ServiceQueryParams queryParams)
        {
            try
            {
                var (services, total) = await _serviceDao.GetAllActiveServicesAsync(queryParams);

                var dtos = services.Select(s => new ServiceResponseDto
                {
                    ServiceId = s.ServiceId,
                    ServiceName = s.ServiceName,
                    Description = s.Description,
                    BasePrice = s.BasePrice,
                    EstimatedTime = s.EstimatedTime,
                    Status = Enum.Parse<ServiceStatus>(s.Status),
                    ReminderIntervalDays = s.ReminderIntervalDays!.Value,
                    ReminderMileage = s.ReminderMileage!.Value,
                    Notes = s.Notes,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt
                }).ToList();

                var responseData = new { services = dtos, total, page = queryParams.Page, pageSize = queryParams.PageSize };
                return Ok(new ApiResponse<object>(200, "Success", "Active services retrieved successfully.", data: responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to retrieve active services: {ex.Message}"));
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateService(int id, [FromBody] ServiceUpdateRequestDto dto)
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
                if (id != dto.ServiceId)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Service ID mismatch."));

                var service = new Service
                {
                    ServiceId = dto.ServiceId,
                    ServiceName = dto.ServiceName,
                    Description = dto.Description,
                    BasePrice = dto.BasePrice,
                    EstimatedTime = dto.EstimatedTime,
                    Status = dto.Status.ToString(),
                    ReminderIntervalDays = dto.ReminderIntervalDays,
                    ReminderMileage = dto.ReminderMileage,
                    Notes = dto.Notes
                };

                var updatedService = await _serviceDao.UpdateServiceAsync(service);
                var updatedDto = new ServiceResponseDto
                {
                    ServiceId = updatedService.ServiceId,
                    ServiceName = updatedService.ServiceName,
                    Description = updatedService.Description,
                    BasePrice = updatedService.BasePrice,
                    EstimatedTime = updatedService.EstimatedTime,
                    Status = Enum.Parse<ServiceStatus>(updatedService.Status),
                    ReminderIntervalDays = updatedService.ReminderIntervalDays!.Value,
                    ReminderMileage = updatedService.ReminderMileage!.Value,
                    Notes = updatedService.Notes,
                    CreatedAt = updatedService.CreatedAt,
                    UpdatedAt = updatedService.UpdatedAt
                };

                return Ok(new ApiResponse<ServiceResponseDto>(200, "Success", "Service updated successfully.", data: updatedDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to update service: {ex.Message}"));
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteService(int id)
        {
            try
            {
                var success = await _serviceDao.DeleteServiceAsync(id);
                if (!success)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Service not found."));

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to delete service: {ex.Message}"));
            }
        }


        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAllServices([FromQuery] ServiceQueryParams queryParams)
        {
            try
            {
                var (services, total) = await _serviceDao.GetAllServicesAsync(queryParams);

                var dtos = services.Select(s => new ServiceResponseDto
                {
                    ServiceId = s.ServiceId,
                    ServiceName = s.ServiceName,
                    Description = s.Description,
                    BasePrice = s.BasePrice,
                    EstimatedTime = s.EstimatedTime,
                    Status = Enum.Parse<ServiceStatus>(s.Status),
                    ReminderIntervalDays = s.ReminderIntervalDays ?? 0,
                    ReminderMileage = s.ReminderMileage ?? 0,
                    Notes = s.Notes,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt
                }).ToList();

                var responseData = new { services = dtos, total, page = queryParams.Page, pageSize = queryParams.PageSize };
                return Ok(new ApiResponse<object>(200, "Success", "Services retrieved successfully.", data: responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", $"Failed to retrieve services: {ex.Message}"));
            }
        }
    }
}
