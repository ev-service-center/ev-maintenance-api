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
    public class PartController : ControllerBase
    {
        private readonly PartDao _partDao;

        public PartController(PartDao partDao)
        {
            _partDao = partDao;
        }

        [HttpPost]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> CreatePart([FromBody] PartCreateRequestDto dto)
        {
            try
            {
                var part = new Part
                {
                    PartName = dto.PartName,
                    Description = dto.Description,
                    Price = dto.Price,
                    QuantityInStock = dto.QuantityInStock,
                    MinStock = dto.MinStock,
                    CenterId = dto.CenterId,
                    Status = dto.Status.ToString()
                };

                var createdPart = await _partDao.CreatePartAsync(part);
                var createdDto = new PartResponseDto
                {
                    PartId = createdPart.PartId,
                    PartName = createdPart.PartName,
                    Description = createdPart.Description,
                    Price = createdPart.Price,
                    QuantityInStock = createdPart.QuantityInStock.Value,
                    MinStock = createdPart.MinStock.Value,
                    CenterId = createdPart.CenterId,
                    Status = Enum.Parse<PartStatus>(createdPart.Status),
                    CreatedAt = createdPart.CreatedAt,
                    UpdatedAt = createdPart.UpdatedAt
                };

                return CreatedAtAction(nameof(GetPart), new { id = createdPart.PartId }, new ApiResponse<PartResponseDto>(201, "Created", "Part created successfully.", data: createdDto));
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
                var part = await _partDao.GetPartByIdAsync(id);
                if (part == null)
                    return NotFound(new ApiResponse<PartResponseDto>(404, "NotFound", "Part not found."));

                var dto = new PartResponseDto
                {
                    PartId = part.PartId,
                    PartName = part.PartName,
                    Description = part.Description,
                    Price = part.Price,
                    QuantityInStock = part.QuantityInStock.Value,
                    MinStock = part.MinStock.Value,
                    CenterId = part.CenterId,
                    Status = Enum.Parse<PartStatus>(part.Status),
                    CreatedAt = part.CreatedAt,
                    UpdatedAt = part.UpdatedAt
                };

                return Ok(new ApiResponse<PartResponseDto>(200, "Success", "Part retrieved successfully.", data: dto));
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
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> UpdatePart(int id, [FromBody] PartUpdateRequestDto dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Empty request (DTO null)."));
                }

                if (id != dto.PartId)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Part ID mismatch."));

                var part = new Part
                {
                    PartId = dto.PartId,
                    PartName = dto.PartName,
                    Description = dto.Description,
                    Price = dto.Price,
                    QuantityInStock = dto.QuantityInStock,
                    MinStock = dto.MinStock,
                    CenterId = dto.CenterId,
                    Status = dto.Status.ToString()
                };

                var updatedPart = await _partDao.UpdatePartAsync(part);
                var updatedDto = new PartResponseDto
                {
                    PartId = updatedPart.PartId,
                    PartName = updatedPart.PartName,
                    Description = updatedPart.Description,
                    Price = updatedPart.Price,
                    QuantityInStock = updatedPart.QuantityInStock!.Value,
                    MinStock = updatedPart.MinStock!.Value,
                    CenterId = updatedPart.CenterId,
                    Status = Enum.Parse<PartStatus>(updatedPart.Status!),
                    CreatedAt = updatedPart.CreatedAt,
                    UpdatedAt = updatedPart.UpdatedAt
                };

                return Ok(new ApiResponse<PartResponseDto>(200, "Success", "Part updated successfully.", data: updatedDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
    }
}
