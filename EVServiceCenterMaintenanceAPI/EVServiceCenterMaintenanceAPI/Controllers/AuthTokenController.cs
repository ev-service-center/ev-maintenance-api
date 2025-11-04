using System;
using System.Threading.Tasks;
using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.DTO;
using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EVServiceCenterMaintenanceAPI.Params;
using EVServiceCenterMaintenanceAPI.Enums;
using Newtonsoft.Json.Linq;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthTokenController : ControllerBase
    {
        private readonly AuthTokenDao _authTokenDao;

        public AuthTokenController(AuthTokenDao authTokenDao)
        {
            _authTokenDao = authTokenDao;
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateAuthToken([FromBody] AuthTokenCreateRequestDto dto)
        {
            try
            {
                var token = new AuthToken
                {
                    UserId = dto.UserId,
                    TokenType = dto.TokenType.ToString(),
                    TokenValue = dto.TokenValue,
                    ExpiresAt = dto.ExpiresAt
                };

                var createdToken = await _authTokenDao.CreateAuthTokenAsync(token);
                var createdDto = new AuthTokenResponseDto
                {
                    TokenId = createdToken.TokenId,
                    UserId = createdToken.UserId,
                    TokenType = Enum.Parse<TokenType>(createdToken.TokenType),
                    TokenValue = createdToken.TokenValue,
                    CreatedAt = createdToken.CreatedAt,
                    ExpiresAt = createdToken.ExpiresAt,
                    IsUsed = createdToken.IsUsed,
                    UpdatedAt = createdToken.UpdatedAt
                };

                return CreatedAtAction(nameof(GetAuthToken), new { id = createdToken.TokenId }, new ApiResponse<AuthTokenResponseDto>(201, "Created", "Auth token created successfully.", data: createdDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAuthToken(int id)
        {
            try
            {
                var token = await _authTokenDao.GetAuthTokenByIdAsync(id);
                if (token == null)
                    return NotFound(new ApiResponse<AuthTokenResponseDto>(404, "NotFound", "Auth token not found."));

                var dto = new AuthTokenResponseDto
                {
                    TokenId = token.TokenId,
                    UserId = token.UserId,
                    TokenType = Enum.Parse<TokenType>(token.TokenType),
                    TokenValue = token.TokenValue,
                    CreatedAt = token.CreatedAt,
                    ExpiresAt = token.ExpiresAt,
                    IsUsed = token.IsUsed,
                    UpdatedAt = token.UpdatedAt
                };

                return Ok(new ApiResponse<AuthTokenResponseDto>(200, "Success", "Auth token retrieved successfully.", data: dto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllAuthTokens([FromQuery] AuthTokenQueryParams queryParams)
        {
            try
            {
                var (tokens, total) = await _authTokenDao.GetAllAuthTokensAsync(queryParams);

                var dtos = tokens.Select(t => new AuthTokenResponseDto
                {
                    TokenId = t.TokenId,
                    UserId = t.UserId,
                    TokenType = Enum.Parse<TokenType>(t.TokenType),
                    TokenValue = t.TokenValue,
                    CreatedAt = t.CreatedAt,
                    ExpiresAt = t.ExpiresAt,
                    IsUsed = t.IsUsed,
                    UpdatedAt = t.UpdatedAt
                }).ToList();

                var responseData = new { tokens = dtos, total, page = queryParams.Page, pageSize = queryParams.PageSize };
                return Ok(new ApiResponse<object>(200, "Success", "Auth tokens retrieved successfully.", data: responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAuthToken(int id)
        {
            try
            {
                var success = await _authTokenDao.DeleteAuthTokenAsync(id);
                if (!success)
                    return NotFound(new ApiResponse<object>(404, "NotFound", "Auth token not found."));

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
        }
    }
}