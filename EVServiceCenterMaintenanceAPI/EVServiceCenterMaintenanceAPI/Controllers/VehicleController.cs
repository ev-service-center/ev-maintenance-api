using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.Params;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VehicleController : ControllerBase
    {
        private readonly VehicleDao _vehicleDao;
        private readonly ReminderDao _reminderDao;
        private readonly ImageService _imageService;

        public VehicleController(VehicleDao vehicleDao, ReminderDao reminderDao, ImageService imageService)
        {
            _vehicleDao = vehicleDao;
            _reminderDao = reminderDao;
            _imageService = imageService;
        }

        [HttpPost]
        [Authorize(Roles = "Customer,Staff,Admin")]
        public async Task<IActionResult> CreateVehicle([FromBody] VehicleCreateRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(kvp => !string.IsNullOrEmpty(kvp.Key) && kvp.Key != "id" && kvp.Value?.Errors?.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? []);
                    return BadRequest(new ApiResponse<object>(400, "Validation Error", "One or more validation errors occurred.", errors));
                }

                var vehicle = new Vehicle
                {
                    CustomerId = dto.CustomerId,
                    Model = dto.Model,
                    Vin = dto.VIN,
                    ManufactureYear = dto.ManufactureYear,
                    CurrentMileage = dto.CurrentMileage,
                    Color = dto.Color,
                    Plate = dto.Plate
                };

                var createdVehicle = await _vehicleDao.CreateVehicleAsync(vehicle);
                var createdDto = new VehicleResponeDto
                {
                    VehicleId = createdVehicle.VehicleId,
                    CustomerId = createdVehicle.CustomerId,
                    Model = createdVehicle.Model,
                    VIN = createdVehicle.Vin,
                    ManufactureYear = createdVehicle.ManufactureYear,
                    CurrentMileage = createdVehicle.CurrentMileage!.Value,
                    LastMaintenanceDate = createdVehicle.LastMaintenanceDate,
                    Color = createdVehicle.Color,
                    Plate = createdVehicle.Plate,
                    CreatedAt = createdVehicle.CreatedAt,
                    UpdatedAt = createdVehicle.UpdatedAt
                };

                await _reminderDao.GenerateRemindersForVehicleAsync(createdVehicle.VehicleId);

                return CreatedAtAction(nameof(GetVehicle),
                    new { id = createdVehicle.VehicleId },
                    new ApiResponse<VehicleResponeDto>(201, "Created", "Vehicle created successfully.", data: createdDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetVehicle(int id)
        {
            try
            {
                var vehicle = await _vehicleDao.GetVehicleByIdAsync(id);
                if (vehicle == null)
                    return NotFound(new ApiResponse<VehicleResponeDto>(404, "NotFound", "Vehicle not found."));

                var dto = new VehicleResponeDto
                {
                    VehicleId = vehicle.VehicleId,
                    CustomerId = vehicle.CustomerId,
                    Model = vehicle.Model,
                    VIN = vehicle.Vin,
                    ManufactureYear = vehicle.ManufactureYear,
                    CurrentMileage = vehicle.CurrentMileage!.Value,
                    LastMaintenanceDate = vehicle.LastMaintenanceDate,
                    Color = vehicle.Color,
                    Plate = vehicle.Plate,
                    CreatedAt = vehicle.CreatedAt,
                    UpdatedAt = vehicle.UpdatedAt
                };

                return Ok(new ApiResponse<VehicleResponeDto>(200, "Success", "Vehicle retrieved successfully.", data: dto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        } 

        [HttpGet("customer/{customerId}")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetVehiclesByCustomer(int customerId)
        {
            try
            {
                var vehicles = await _vehicleDao.GetVehiclesByCustomerIdAsync(customerId);
                var dtos = vehicles.Select(v => new VehicleResponeDto
                {
                    VehicleId = v.VehicleId,
                    CustomerId = v.CustomerId,
                    Model = v.Model,
                    VIN = v.Vin,
                    ManufactureYear = v.ManufactureYear,
                    CurrentMileage = v.CurrentMileage!.Value,
                    LastMaintenanceDate = v.LastMaintenanceDate,
                    Color = v.Color,
                    Plate = v.Plate,
                    CreatedAt = v.CreatedAt,
                    UpdatedAt = v.UpdatedAt
                }).ToList();

                return Ok(new ApiResponse<List<VehicleResponeDto>>(200, "Success", "Vehicles retrieved successfully.", data: dtos));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> GetAllVehicles([FromQuery] VehicleQueryParams queryParams)
        {
            try
            {
                var (vehicles, total) = await _vehicleDao.GetAllVehiclesAsync(queryParams);

                var dtos = vehicles.Select(v => new VehicleResponeDto
                {
                    VehicleId = v.VehicleId,
                    CustomerId = v.CustomerId,
                    Model = v.Model,
                    VIN = v.Vin,
                    ManufactureYear = v.ManufactureYear,
                    CurrentMileage = v.CurrentMileage!.Value,
                    LastMaintenanceDate = v.LastMaintenanceDate,
                    Color = v.Color,
                    Plate = v.Plate,
                    CreatedAt = v.CreatedAt,
                    UpdatedAt = v.UpdatedAt
                }).ToList();

                var responseData = new { vehicles = dtos, total, page = queryParams.Page, pageSize = queryParams.PageSize };
                return Ok(new ApiResponse<object>(200, "Success", "Vehicles retrieved successfully.", data: responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Customer,Staff,Admin")]
        public async Task<IActionResult> UpdateVehicle(int id, [FromBody] VehicleUpdateRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(kvp => !string.IsNullOrEmpty(kvp.Key) && kvp.Key != "id" && kvp.Value?.Errors?.Count > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? []);
                    return BadRequest(new ApiResponse<object>(400, "Validation Error", "One or more validation errors occurred.", errors));
                }

                if (id != dto.VehicleId)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Vehicle ID mismatch."));

                var existingVehicle = await _vehicleDao.GetVehicleByIdAsync(id);
                if (existingVehicle == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Vehicle not found."));

                var vehicle = new Vehicle
                {
                    VehicleId = dto.VehicleId,
                    CustomerId = dto.CustomerId,
                    Model = dto.Model,
                    Vin = dto.VIN,
                    ManufactureYear = dto.ManufactureYear,
                    CurrentMileage = dto.CurrentMileage,
                    Color = dto.Color,
                    Plate = dto.Plate,
                    CreatedAt = existingVehicle.CreatedAt,
                    UpdatedAt = DateTime.UtcNow
                };

                var updatedVehicle = await _vehicleDao.UpdateVehicleAsync(vehicle);
                var updatedDto = new VehicleResponeDto
                {
                    VehicleId = updatedVehicle.VehicleId,
                    CustomerId = updatedVehicle.CustomerId,
                    Model = updatedVehicle.Model,
                    VIN = updatedVehicle.Vin,
                    ManufactureYear = updatedVehicle.ManufactureYear,
                    CurrentMileage = updatedVehicle.CurrentMileage!.Value,
                    LastMaintenanceDate = updatedVehicle.LastMaintenanceDate,
                    Color = updatedVehicle.Color,
                    Plate = updatedVehicle.Plate,
                    CreatedAt = updatedVehicle.CreatedAt,
                    UpdatedAt = updatedVehicle.UpdatedAt
                };

                await _reminderDao.GenerateRemindersForVehicleAsync(updatedVehicle.VehicleId);

                return Ok(new ApiResponse<VehicleResponeDto>(200, "Success", "Vehicle updated successfully.", data: updatedDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteVehicle(int id)
        {
            try
            {
                var vehicle = await _vehicleDao.GetVehicleByIdAsync(id);
                if (vehicle == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Vehicle not found."));

                var success = await _vehicleDao.DeleteVehicleAsync(id);
                if (!success)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Vehicle not found."));

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
    }
}
