using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
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
    }
}
