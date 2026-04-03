
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using PeaceEnablers.Dtos.CountryDto;

namespace PeaceEnablers.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "StaffOnly")]
    public class CountryController : ControllerBase
    {
        private readonly ICountryService _countryService;
        public CountryController(ICountryService CountryService)
        {
            _countryService = CountryService;
        }
        private int? GetUserIdFromClaims()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;

            return null;
        }
        private string? GetTierFromClaims()
        {
            return User.FindFirst("Tier")?.Value;
        }
        private string? GetRoleFromClaims()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value;
        }

        [HttpGet("countries")]
        public async Task<IActionResult> GetCountries([FromQuery] PaginationRequest request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            request.UserId = userId;
            return Ok(await _countryService.GetCountriesAsync(request, userRole));
        }

        [HttpGet("getAllCountryByUserId/{userId}")]
        public async Task<IActionResult> getAllCountryByUserId(int userId)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null)
                return Unauthorized("User ID not found.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _countryService.getAllCountryByUserId(claimUserId.GetValueOrDefault(), userRole));
        }

        [HttpGet("countries/{id}")]
        public async Task<IActionResult> GetByIdAsync(int id) => Ok(await _countryService.GetByIdAsync(id));

        [HttpPost("AddUpdateCountry")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddUpdateCountry([FromForm] AddUpdateCountryDto q)
        {
            var result = await _countryService.AddUpdateCountry(q);
            return Ok(result);
        }

        [HttpPost("addBulkCountry")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddBulkCountry([FromBody] BulkAddCountryDto q)
        {
            var result = await _countryService.AddBulkCountryAsync(q);
            return Ok(result);
        }

        [HttpPut("edit/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditCountry(int id, [FromBody] AddUpdateCountryDto q)
        {
            var result = await _countryService.EditCountryAsync(id, q);
            return Ok(result);
        }

        [HttpDelete("delete/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCountry(int id)
        {
            var success = await _countryService.DeleteCountryAsync(id);
            return Ok(success);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("assignCountry")]
        public async Task<IActionResult> AssignCountry([FromBody] UserCountryMappingRequestDto q)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found.");

            q.UserId = userId.Value;
            var result = await _countryService.AssingCountryToUser(q.UserId, q.CountryId, q.AssignedByUserId);
            return Ok(result);
        }

        [HttpPut]
        [Authorize(Roles = "Admin")]
        [Route("assignCountry/{id}")]
        public async Task<IActionResult> EditAssignCountry(int id, [FromBody] UserCountryMappingRequestDto q)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null || claimUserId != q.AssignedByUserId)
                return Unauthorized("User ID not found.");

            var result = await _countryService.EditAssingCountry(id, q.UserId,q.CountryId,q.AssignedByUserId);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        [Authorize]
        [Route("unAssignCountry")]
        public async Task<IActionResult> UnAssignCountry([FromBody] UserCountryUnMappingRequestDto requestDto)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null || claimUserId != requestDto.AssignedByUserId)
                return Unauthorized("User ID not found.");

            var result = await _countryService.UnAssignCountry(requestDto);
            return Ok(result);
        }

        [HttpGet]
        [Authorize]
        [Route("getCountryByUserIdForAssessment/{userId}")]
        public async Task<IActionResult> GetCountryByUserIdForAssessment(int userId)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null || claimUserId != userId)
                return Unauthorized("User ID not found.");

            var result = await _countryService.GetCountryByUserIdForAssessment(userId);
            return Ok(result);
        }

        [HttpGet]
        [Authorize]
        [Route("getCountryHistory/{updatedAt}")]
        public async Task<IActionResult> GetCountryHistory(DateTime updatedAt)
        {
            var claimUserId = GetUserIdFromClaims();                                    
            if (claimUserId == null)
                return Unauthorized("User ID not found.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            var result = await _countryService.GetCountryHistory(claimUserId.GetValueOrDefault(), updatedAt, userRole);
            return Ok(result);
        }

        [HttpGet]
        [Authorize]
        [Route("getCountriesProgressByUserId/{updatedAt}")]
        public async Task<IActionResult> getCountriesProgressByUserId(DateTime updatedAt)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found.");
            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            var result = await _countryService.GetCountriesProgressByUserId(userId.GetValueOrDefault(), updatedAt, userRole);
            return Ok(result);
        }

        [HttpGet("getAllCountryByLocation")]
        public async Task<IActionResult> GetAllCountryByLocation([FromQuery] GetNearestCountryRequestDto r) => Ok(await _countryService.getAllCountryByLocation(r));
         
        [HttpGet("getAiAccessCountry")]
        public async Task<IActionResult> GetAiAccessCountry()
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null)
                return Unauthorized("User ID not found.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _countryService.GetAiAccessCountry(claimUserId.GetValueOrDefault(), userRole));
        }

        [HttpGet("exportCountries")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportCountries([FromQuery] ExportCountryWithOptionDto request)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null)
                return Unauthorized("User ID not found.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            var result = await _countryService.ExportCountries(request, claimUserId.GetValueOrDefault(), userRole);

            if (!result.Succeeded)
                return BadRequest(result.Messages);

            string fileName = $"Cities_Progress_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

            return File(
                result.Result ?? new byte[1],
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName
            );
        }

    }
}
