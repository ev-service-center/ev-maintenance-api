using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.Params;
using EVServiceCenterMaintenanceAPI.Enums;
using System.Security.Claims;
using EVServiceCenterMaintenanceAPI.Utils;

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

                // Get current user info
                var userId = JwtHelper.GetUserIdFromHttpContext(HttpContext);
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

                // Security: Customer can only create vehicles for themselves
                if (userRole == UserRole.Customer.ToString())
                {
                    if (dto.CustomerId != userId)
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden", "Customers can only create vehicles for themselves."));
                }
                // Staff and Admin can create vehicles for any customer

                // Check if VIN already exists in Active vehicles (filtered unique index)
                bool isVinExists = await _vehicleDao.IsVinExistsInActiveVehiclesAsync(dto.VIN);
                if (isVinExists)
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "A vehicle with this VIN already exists in the system."));
                }

                // Check if Plate already exists in Active vehicles (filtered unique index)
                bool isPlateExists = await _vehicleDao.IsPlateExistsInActiveVehiclesAsync(dto.Plate);
                if (isPlateExists)
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "A vehicle with this license plate already exists in the system."));
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
                    Status = Enum.Parse<VehicleStatus>(createdVehicle.Status),
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

                // Get current user info
                var userId = JwtHelper.GetUserIdFromHttpContext(HttpContext);
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

                // Security: Customer can only view their own vehicles
                if (userRole == UserRole.Customer.ToString())
                {
                    if (vehicle.CustomerId != userId)
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden", "You can only view your own vehicles."));
                }
                // Staff and Admin can view all vehicles

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
                    Status = Enum.Parse<VehicleStatus>(vehicle.Status),
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
        [Authorize(Roles = "Customer,Staff,Admin")]
        public async Task<IActionResult> GetVehiclesByCustomer(int customerId)
        {
            try
            {
                // Get current user info
                var userId = JwtHelper.GetUserIdFromHttpContext(HttpContext);
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

                // Security: Customer can only view their own vehicles
                if (userRole == UserRole.Customer.ToString())
                {
                    if (customerId != userId)
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden", "You can only view your own vehicles."));
                }
                // Staff and Admin can view vehicles for any customer

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
                    Status = Enum.Parse<VehicleStatus>(v.Status),
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
                    Status = Enum.Parse<VehicleStatus>(v.Status),
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

                var existingVehicle = await _vehicleDao.GetVehicleByIdAsync(id);
                if (existingVehicle == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Vehicle not found."));

                // Get current user info
                var userId = JwtHelper.GetUserIdFromHttpContext(HttpContext);
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

                // Store original status for validation logic
                var originalStatus = existingVehicle.Status;

                // If user is Customer, check ownership
                if (userRole == UserRole.Customer.ToString())
                {
                    if (existingVehicle.CustomerId != userId)
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden", "You can only update your own vehicles."));

                    // Customer CANNOT update Status - business logic: prevent self-reactivation of sold vehicles
                    if (dto.Status.HasValue)
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden", "Customers cannot change vehicle status. Please contact support if you need to reactivate a deleted vehicle."));
                }
                // Staff and Admin can update Status
                else
                {
                    // Validate Status enum if provided by Staff/Admin
                    if (dto.Status.HasValue && !Enum.IsDefined(typeof(VehicleStatus), dto.Status.Value))
                    {
                        return BadRequest(new ApiResponse<object>(400, "BadRequest", "Invalid status."));
                    }

                    // Check if reactivating (Inactive → Active)
                    if (dto.Status.HasValue &&
                        dto.Status.Value == VehicleStatus.Active &&
                        originalStatus == VehicleStatus.Inactive.ToString())
                    {
                        // Check if VIN or Plate conflicts with another Active vehicle
                        bool isVinConflict = await _vehicleDao.IsVinExistsInActiveVehiclesAsync(existingVehicle.Vin, id);
                        if (isVinConflict)
                        {
                            return BadRequest(new ApiResponse<object>(400, "BadRequest",
                                "Cannot reactivate: Another active vehicle with this VIN already exists. The vehicle may have been registered by a new owner."));
                        }

                        bool isPlateConflict = await _vehicleDao.IsPlateExistsInActiveVehiclesAsync(existingVehicle.Plate, id);
                        if (isPlateConflict)
                        {
                            return BadRequest(new ApiResponse<object>(400, "BadRequest",
                                "Cannot reactivate: Another active vehicle with this license plate already exists."));
                        }
                    }
                }

                // Determine final status (either updated or original)
                var finalStatus = dto.Status.HasValue ? dto.Status.Value.ToString() : originalStatus;

                // Check VIN conflict if updating VIN
                if (dto.VIN != null && dto.VIN != existingVehicle.Vin)
                {
                    // Only check if vehicle is Active or will be Active
                    if (finalStatus == VehicleStatus.Active.ToString())
                    {
                        bool isVinExists = await _vehicleDao.IsVinExistsInActiveVehiclesAsync(dto.VIN, id);
                        if (isVinExists)
                        {
                            return BadRequest(new ApiResponse<object>(400, "BadRequest", "A vehicle with this VIN already exists in the system."));
                        }
                    }
                }

                // Check Plate conflict if updating Plate
                if (dto.Plate != null && dto.Plate != existingVehicle.Plate)
                {
                    // Only check if vehicle is Active or will be Active
                    if (finalStatus == VehicleStatus.Active.ToString())
                    {
                        bool isPlateExists = await _vehicleDao.IsPlateExistsInActiveVehiclesAsync(dto.Plate, id);
                        if (isPlateExists)
                        {
                            return BadRequest(new ApiResponse<object>(400, "BadRequest", "A vehicle with this license plate already exists in the system."));
                        }
                    }
                }

                // All validations passed - apply changes
                if (dto.Status.HasValue)
                    existingVehicle.Status = dto.Status.Value.ToString();

                if (dto.Model != null)
                    existingVehicle.Model = dto.Model;

                if (dto.VIN != null)
                    existingVehicle.Vin = dto.VIN;

                if (dto.ManufactureYear.HasValue)
                    existingVehicle.ManufactureYear = dto.ManufactureYear;

                if (dto.CurrentMileage.HasValue)
                    existingVehicle.CurrentMileage = dto.CurrentMileage;

                if (dto.Color != null)
                    existingVehicle.Color = dto.Color;

                if (dto.Plate != null)
                    existingVehicle.Plate = dto.Plate;

                existingVehicle.UpdatedAt = DateTime.UtcNow;

                var updatedVehicle = await _vehicleDao.UpdateVehicleAsync(existingVehicle);
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
                    Status = Enum.Parse<VehicleStatus>(updatedVehicle.Status),
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
        [Authorize(Roles = "Customer,Staff,Admin")]
        public async Task<IActionResult> DeleteVehicle(int id)
        {
            try
            {
                var vehicle = await _vehicleDao.GetVehicleByIdAsync(id);
                if (vehicle == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Vehicle not found."));

                // Get current user info
                var userId = JwtHelper.GetUserIdFromHttpContext(HttpContext);
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

                // If user is Customer, check ownership
                if (userRole == UserRole.Customer.ToString())
                {
                    if (vehicle.CustomerId != userId)
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden", "You can only delete your own vehicles."));
                }
                // Staff and Admin can delete any vehicle

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
