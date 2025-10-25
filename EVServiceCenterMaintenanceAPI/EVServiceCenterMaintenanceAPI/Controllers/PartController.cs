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
