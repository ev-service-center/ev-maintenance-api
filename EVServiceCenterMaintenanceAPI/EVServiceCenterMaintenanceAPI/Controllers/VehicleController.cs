using System;
using System.Threading.Tasks;
using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EVServiceCenterMaintenanceAPI.Utils;
using EVServiceCenterMaintenanceAPI.Params;
using EVServiceCenterMaintenanceAPI.Services;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VehicleController : ControllerBase
    {
        private readonly VehicleDao _vehicleDao;  
        public VehicleController(VehicleDao vehicleDao, ImageService imageService)
        {
            _vehicleDao = vehicleDao;
        }

       

        [HttpGet]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> GetAllVehicles([FromQuery] VehicleQueryParams queryParams)
        {
            try
            {
                var (vehicles, total) = await _vehicleDao.GetAllVehiclesAsync(queryParams);

                var dtos = vehicles.Select(v => new VehicleResponseDto
                {
                    VehicleId = v.VehicleId,
                    CustomerId = v.CustomerId,
                    Model = v.Model,
                    VIN = v.Vin,
                    ManufactureYear = v.ManufactureYear,
                    CurrentMileage = v.CurrentMileage.Value,
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
    }
}