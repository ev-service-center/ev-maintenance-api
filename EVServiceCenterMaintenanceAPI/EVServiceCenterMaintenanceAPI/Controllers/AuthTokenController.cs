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
    public class AuthTokenController : ControllerBase
    {
        private readonly AuthTokenDao _authTokenDao;

        public AuthTokenController(AuthTokenDao authTokenDao)
        {
            _authTokenDao = authTokenDao;
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

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateAuthToken(int id, [FromBody] AuthTokenUpdateRequestDto dto)
        {
            try
            {
                if (id != dto.TokenId)
                    return BadRequest(new ApiResponse<object>(400, "BadRequest", "Auth token ID mismatch."));

                var token = new AuthToken
                {
                    TokenId = dto.TokenId,
                    UserId = dto.UserId,
                    TokenType = dto.TokenType.ToString(),
                    TokenValue = dto.TokenValue,
                    ExpiresAt = dto.ExpiresAt,
                    IsUsed = dto.IsUsed
                };

                var updatedToken = await _authTokenDao.UpdateAuthTokenAsync(token);
                var updatedDto = new AuthTokenResponseDto
                {
                    TokenId = updatedToken.TokenId,
                    UserId = updatedToken.UserId,
                    TokenType = Enum.Parse<TokenType>(updatedToken.TokenType),
                    TokenValue = updatedToken.TokenValue,
                    CreatedAt = updatedToken.CreatedAt,
                    ExpiresAt = updatedToken.ExpiresAt,
                    IsUsed = updatedToken.IsUsed,
                    UpdatedAt = updatedToken.UpdatedAt
                };

                return Ok(new ApiResponse<AuthTokenResponseDto>(200, "Success", "Auth token updated successfully.", data: updatedDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>(500, "Error", ex.Message));
            }
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


    }
}
