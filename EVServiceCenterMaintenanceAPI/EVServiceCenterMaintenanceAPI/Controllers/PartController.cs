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
    public class PartController : ControllerBase
    {
        private readonly PartDao _partDao;
        private readonly UserDao _userDao;
        private readonly EmployeeDao _employeeDao;
        private readonly ServiceCenterDao _serviceCenterDao;

        public PartController(PartDao partDao, UserDao userDao, EmployeeDao employeeDao, ServiceCenterDao serviceCenterDao)
        {
            _partDao = partDao;
            _userDao = userDao;
            _employeeDao = employeeDao;
            _serviceCenterDao = serviceCenterDao;
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreatePart([FromBody] PartCreateRequestDto dto)
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

                var part = new Part
                {
                    PartName = dto.PartName,
                    Description = dto.Description,
                    CostPrice = dto.CostPrice,
                    Price = dto.Price,
                    QuantityInStock = dto.QuantityInStock,
                    MinStock = dto.MinStock,
                    CenterId = dto.CenterId,
                    Status = dto.Status.ToString()
                };

                var createdPart = await _partDao.CreatePartAsync(part);

                var adminDto = new PartAdminResponseDto
                {
                    PartId = createdPart.PartId,
                    PartName = createdPart.PartName,
                    Description = createdPart.Description,
                    CostPrice = createdPart.CostPrice,
                    Price = createdPart.Price,
                    QuantityInStock = createdPart.QuantityInStock ?? 0,
                    MinStock = createdPart.MinStock ?? 0,
                    CenterId = createdPart.CenterId,
                    Status = string.IsNullOrEmpty(createdPart.Status)
                        ? PartStatus.Inactive
                        : Enum.TryParse<PartStatus>(createdPart.Status, out var status)
                            ? status
                            : PartStatus.Inactive,
                    CreatedAt = createdPart.CreatedAt,
                    UpdatedAt = createdPart.UpdatedAt
                };

                return CreatedAtAction(nameof(GetPart), new { id = createdPart.PartId }, new ApiResponse<PartAdminResponseDto>(201, "Created", "Part created successfully.", data: adminDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Staff,Technician,Admin")]
        public async Task<IActionResult> GetPart(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst("UserId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                var currentUser = await _userDao.GetUserByIdAsync(currentUserId);
                if (currentUser == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "User not found."));

                var part = await _partDao.GetPartByIdAsync(id);
                if (part == null)
                    return NotFound(new ApiResponse<PartResponseDto>(404, "NotFound", "Part not found."));

                // Staff và Technician chỉ được xem parts tại center của họ
                if (currentUser.Role == UserRole.Staff.ToString() || currentUser.Role == UserRole.Technician.ToString())
                {
                    var currentEmployee = await _employeeDao.GetEmployeeByIdAsync(currentUserId);
                    if (currentEmployee == null)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            $"{currentUser.Role} user does not have an associated employee record."));

                    if (part.CenterId != currentEmployee.CenterId)
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                            $"{currentUser.Role} can only view parts from their own service center (Center ID: {currentEmployee.CenterId})."));
                }

                // Admin gets full details including CostPrice
                if (currentUser.Role == UserRole.Admin.ToString())
                {
                    var adminDto = new PartAdminResponseDto
                    {
                        PartId = part.PartId,
                        PartName = part.PartName,
                        Description = part.Description,
                        CostPrice = part.CostPrice,
                        Price = part.Price,
                        QuantityInStock = part.QuantityInStock ?? 0,
                        MinStock = part.MinStock ?? 0,
                        CenterId = part.CenterId,
                        Status = string.IsNullOrEmpty(part.Status)
                            ? PartStatus.Inactive
                            : Enum.TryParse<PartStatus>(part.Status, out var status)
                                ? status
                                : PartStatus.Inactive,
                        CreatedAt = part.CreatedAt,
                        UpdatedAt = part.UpdatedAt
                    };
                    return Ok(new ApiResponse<PartAdminResponseDto>(200, "Success", "Part retrieved successfully.", data: adminDto));
                }
                else
                {
                    // Staff/Technician gets basic details without CostPrice
                    var dto = new PartResponseDto
                    {
                        PartId = part.PartId,
                        PartName = part.PartName,
                        Description = part.Description,
                        Price = part.Price,
                        QuantityInStock = part.QuantityInStock ?? 0,
                        MinStock = part.MinStock ?? 0,
                        CenterId = part.CenterId,
                        Status = string.IsNullOrEmpty(part.Status)
                            ? PartStatus.Inactive
                            : Enum.TryParse<PartStatus>(part.Status, out var status)
                                ? status
                                : PartStatus.Inactive,
                        CreatedAt = part.CreatedAt,
                        UpdatedAt = part.UpdatedAt
                    };
                    return Ok(new ApiResponse<PartResponseDto>(200, "Success", "Part retrieved successfully.", data: dto));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet]
        [Authorize(Roles = "Staff,Technician,Admin")]
        public async Task<IActionResult> GetAllParts([FromQuery] PartQueryParams queryParams)
        {
            try
            {
                var userIdClaim = User.FindFirst("UserId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                    return Unauthorized(new ApiResponse<object>(401, "Unauthorized", "Invalid user ID."));

                var currentUser = await _userDao.GetUserByIdAsync(currentUserId);
                if (currentUser == null)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "User not found."));

                // Validate CenterId tồn tại ngay từ đầu nếu có trong query params
                if (queryParams.CenterId.HasValue)
                {
                    var centerExists = await _serviceCenterDao.IsExistServiceCenterAsync(queryParams.CenterId.Value);
                    if (!centerExists)
                        return NotFound(new ApiResponse<object>(404, "NotFound", $"Service Center with ID {queryParams.CenterId.Value} not found."));
                }

                if (currentUser.Role == UserRole.Staff.ToString() || currentUser.Role == UserRole.Technician.ToString())
                {
                    // Staff và Technician chỉ được xem parts tại center của họ
                    var currentEmployee = await _employeeDao.GetEmployeeByIdAsync(currentUserId);
                    if (currentEmployee == null)
                        return BadRequest(new ApiResponse<object>(400, "BadRequest",
                            $"{currentUser.Role} user does not have an associated employee record."));

                    // Nếu có CenterId trong query params và khác center của họ -> Forbidden
                    if (queryParams.CenterId.HasValue && queryParams.CenterId.Value != currentEmployee.CenterId)
                    {
                        return StatusCode(403, new ApiResponse<object>(403, "Forbidden",
                            $"{currentUser.Role} can only view parts from their own service center (Center ID: {currentEmployee.CenterId})."));
                    }
                    queryParams.CenterId = currentEmployee.CenterId;
                }

                var (parts, total) = await _partDao.GetAllPartsAsync(queryParams);

                // Admin gets full details including CostPrice
                if (currentUser.Role == UserRole.Admin.ToString())
                {
                    var adminDtos = parts.Select(p => new PartAdminResponseDto
                    {
                        PartId = p.PartId,
                        PartName = p.PartName,
                        Description = p.Description,
                        CostPrice = p.CostPrice,
                        Price = p.Price,
                        QuantityInStock = p.QuantityInStock ?? 0,
                        MinStock = p.MinStock ?? 0,
                        CenterId = p.CenterId,
                        Status = string.IsNullOrEmpty(p.Status)
                            ? PartStatus.Inactive
                            : Enum.TryParse<PartStatus>(p.Status, out var status)
                                ? status
                                : PartStatus.Inactive,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    }).ToList();

                    var adminResponseData = new { parts = adminDtos, total, page = queryParams.Page, pageSize = queryParams.PageSize };
                    return Ok(new ApiResponse<object>(200, "Success", "Parts retrieved successfully.", data: adminResponseData));
                }
                else
                {
                    // Staff/Technician gets basic details without CostPrice
                    var dtos = parts.Select(p => new PartResponseDto
                    {
                        PartId = p.PartId,
                        PartName = p.PartName,
                        Description = p.Description,
                        Price = p.Price,
                        QuantityInStock = p.QuantityInStock ?? 0,
                        MinStock = p.MinStock ?? 0,
                        CenterId = p.CenterId,
                        Status = string.IsNullOrEmpty(p.Status)
                            ? PartStatus.Inactive
                            : Enum.TryParse<PartStatus>(p.Status, out var status)
                                ? status
                                : PartStatus.Inactive,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    }).ToList();

                    var responseData = new { parts = dtos, total, page = queryParams.Page, pageSize = queryParams.PageSize };
                    return Ok(new ApiResponse<object>(200, "Success", "Parts retrieved successfully.", data: responseData));
                }
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

        [HttpGet("suggestions")]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> GetPartReorderSuggestions([FromQuery] int centerId)
        {
            try
            {
                var suggestions = await _partDao.GetPartReorderSuggestionsAsync(centerId);
                return Ok(new ApiResponse<List<PartSuggestionDto>>(200, "Success", "Part reorder suggestions retrieved successfully.", data: suggestions));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdatePart(int id, [FromBody] PartUpdateRequestDto dto)
        {
            try
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

                if (id != dto.PartId)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Part ID mismatch."));

                var part = new Part
                {
                    PartId = dto.PartId,
                    PartName = dto.PartName,
                    Description = dto.Description,
                    CostPrice = dto.CostPrice,
                    Price = dto.Price,
                    QuantityInStock = dto.QuantityInStock,
                    MinStock = dto.MinStock,
                    CenterId = dto.CenterId,
                    Status = dto.Status.ToString()
                };

                var updatedPart = await _partDao.UpdatePartAsync(part);

                var adminDto = new PartAdminResponseDto
                {
                    PartId = updatedPart.PartId,
                    PartName = updatedPart.PartName,
                    Description = updatedPart.Description,
                    CostPrice = updatedPart.CostPrice,
                    Price = updatedPart.Price,
                    QuantityInStock = updatedPart.QuantityInStock ?? 0,
                    MinStock = updatedPart.MinStock ?? 0,
                    CenterId = updatedPart.CenterId,
                    Status = string.IsNullOrEmpty(updatedPart.Status)
                        ? PartStatus.Inactive
                        : Enum.TryParse<PartStatus>(updatedPart.Status, out var status)
                            ? status
                            : PartStatus.Inactive,
                    CreatedAt = updatedPart.CreatedAt,
                    UpdatedAt = updatedPart.UpdatedAt
                };

                return Ok(new ApiResponse<PartAdminResponseDto>(200, "Success", "Part updated successfully.", data: adminDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeletePart(int id)
        {
            try
            {
                await _partDao.DeletePartAsync(id);
                return Ok(new ApiResponse<object>(200, "Success", "Part deleted successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object>(404, "NotFound", ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
    }
}
