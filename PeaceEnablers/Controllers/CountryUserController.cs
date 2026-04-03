using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Dtos.AssessmentDto;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Dtos.CountryUserDto;
using PeaceEnablers.Enums;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;
using PeaceEnablers.Services;
using System.Security.Claims;

namespace PeaceEnablers.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "PaidCountryUserOnly")]
    public class CountryUserController : ControllerBase
    {
        private readonly ICountryUserService _countryUserService;

        public CountryUserController(ICountryUserService countryUserService)
        {
            _countryUserService = countryUserService;
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

        [HttpGet("getCountryHistory")]
        public async Task<IActionResult> GetCountryHistory()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");
            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            var tier = Enum.Parse<TieredAccessPlan>(tierName);

            var result = await _countryUserService.GetCountryHistory(userId.Value, tier);
            return Ok(result);
        }

        [HttpGet("getCountriesProgressByUserId")]
        public async Task<IActionResult> GetCountriesProgressByUserId()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var result = await _countryUserService.GetCountriesProgressByUserId(userId.Value);
            return Ok(result);
        }

        [HttpGet("getCountryQuestionHistory")]
        public async Task<IActionResult> GetCountryQuestionHistory([FromQuery] UserCountryRequestDto userCountryRequestDto)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            userCountryRequestDto.UserID = userId.Value;
            userCountryRequestDto.Tiered = Enum.Parse<TieredAccessPlan>(tierName);

            var result = await _countryUserService.GetCountryQuestionHistory(userCountryRequestDto);
            return Ok(result);
        }

        [HttpGet("countries")]
        public async Task<IActionResult> GetCountriesAsync([FromQuery] PaginationRequest request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            request.UserId = userId.Value;

            var result = await _countryUserService.GetCountriesAsync(request);
            return Ok(result);
        }

        [HttpGet("getCountryDetails")]
        public async Task<IActionResult> GetCountryDetails([FromQuery] UserCountryRequestDto userCountryRequestDto)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            userCountryRequestDto.UserID = userId.Value;
            userCountryRequestDto.Tiered = Enum.Parse<TieredAccessPlan>(tierName);

            var result = await _countryUserService.GetCountryDetails(userCountryRequestDto);
            return Ok(result);
        }


        [HttpGet("GetCountryPillarDetails")]
        public async Task<IActionResult> GetCountryPillarDetails([FromQuery] UserCountryGetPillarInfoRequestDto userCountryGetPillarInfoRequestDto)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            userCountryGetPillarInfoRequestDto.UserID = userId.Value;
            userCountryGetPillarInfoRequestDto.Tiered = Enum.Parse<TieredAccessPlan>(tierName);

            var result = await _countryUserService.GetCountryPillarDetails(userCountryGetPillarInfoRequestDto);
            return Ok(result);
        }
        [HttpGet("getCountryUserCountries")]
        public async Task<IActionResult> GetCountryUserCountries()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            var response = await _countryUserService.GetCountryUserCountries(userId.Value);
            return Ok(response);
        }
        [HttpPost("addCountryUserKpisCountryAndPillar")]
        public async Task<IActionResult> AddCountryUserKpisCountryAndPillar([FromBody] AddCountryUserKpisCountryAndPillar b)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.IsDefined(typeof(TieredAccessPlan), tierName))
                return Unauthorized("Invalid tier specified.");

            var response = await _countryUserService.AddCountryUserKpisCountryAndPillar(b, userId.GetValueOrDefault(), tierName);
            return Ok(response);
        }
        [HttpGet]
        [Route("getCountryUserKpi")]
        public async Task<IActionResult> GetCountryUserKpi()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            var result = await _countryUserService.GetCountryUserKpi(userId.GetValueOrDefault(), tierName);
            return Ok(result);
        }

        [HttpPost]
        [Route("compareCountries")]
        public async Task<IActionResult> CompareCountries([FromBody] CompareCountryRequestDto r)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            var result = await _countryUserService.CompareCountries(r,userId.GetValueOrDefault(), tierName);
            return Ok(result);
        }

        [HttpGet("getAICountryPillars")]
        public async Task<IActionResult> GetAICountryPillars([FromQuery] AiCountryPillarRequestDto request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            return Ok(await _countryUserService.GetAICountryPillars(request, userId.Value, tierName));
        }

        [HttpGet("ExportCompareCountries")]
        public async Task<IActionResult> ExportCompareCountries(string countries, string? kpis, DateTime updatedAt)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            var countryIds = countries.Split(',')
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(int.Parse)
                .ToList();

            var kpiIds = new List<int>();

            if (!string.IsNullOrWhiteSpace(kpis) && kpis.ToLower() != "null")
            {
                kpiIds = kpis.Split(',')
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(int.Parse)
                    .ToList();
            }

            var request = new CompareCountryRequestDto
            {
                Countries = countryIds,
                Kpis = kpiIds,
                UpdatedAt = updatedAt
            };

            var content = await _countryUserService.ExportCompareCountries(request, userId.Value, tierName);

            return File(content.Item2,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                content.Item1);
        }

        private string? GetRoleFromClaims()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value;
        }

    }
}
