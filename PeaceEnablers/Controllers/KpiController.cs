
using DocumentFormat.OpenXml.Spreadsheet;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using PeaceEnablers.Dtos.CityUserDto;
using PeaceEnablers.Dtos.kpiDto;
using PeaceEnablers.Enums;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;
using PeaceEnablers.Services;

using System.Security.Claims;

namespace PeaceEnablers.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class KpiController : ControllerBase
    {
        private readonly IKpiService _kpiService;

        public KpiController(IKpiService kpiService)
        {
            _kpiService = kpiService;
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

        [HttpGet]
        [Route("GetAnalyticalLayerResults")]
        public async Task<IActionResult> GetAnalyticalLayerResults([FromQuery] GetAnalyticalLayerRequestDto response)
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

            var tierName = GetTierFromClaims();
            if (tierName == null && userRole == UserRole.CityUser)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<TieredAccessPlan>(tierName, true, out var userPlan) && userRole == UserRole.CityUser)
            {
                return Unauthorized("You Don't have access.");
            }

            var result = await _kpiService.GetAnalyticalLayerResults(response, userId.GetValueOrDefault(), userRole, userPlan);
            if (result == null)
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(result);
        }
        [HttpGet]
        [Route("GetAllKpi")]
        public async Task<IActionResult> GetAllKpi()
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

            var result = await _kpiService.GetAllKpi(userId.GetValueOrDefault(), userRole);
            return Ok(result);
        }

        [HttpPost]
        [Route("compareCities")]
        [Authorize(Policy = "StaffOnly")]
        public async Task<IActionResult> CompareCities([FromBody] CompareCityRequestDto r)
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
           var result = await _kpiService.CompareCities(r, userId.GetValueOrDefault(), userRole, true);
            return Ok(result);
        }

        [HttpGet("ExportCompareCities")]
        public async Task<IActionResult> ExportCompareCities( string cities, string? kpis, DateTime updatedAt)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
                return Unauthorized("You Don't have access.");

            var cityIds = cities.Split(',')
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

            var request = new CompareCityRequestDto
            {
                Cities = cityIds,
                Kpis = kpiIds,
                UpdatedAt = updatedAt
            };

            var content = await _kpiService.ExportCompareCities(request, userId.Value, userRole);

            return File(content.Item2,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                content.Item1);
        }


        [HttpPost]
        [Route("getMutiplekpiLayerResults")]
        public async Task<IActionResult> GetMutiplekpiLayerResults([FromBody] GetMutiplekpiLayerRequestDto request)
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

            var tierName = GetTierFromClaims();
            if (tierName == null && userRole == UserRole.CityUser)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<TieredAccessPlan>(tierName, true, out var userPlan) && userRole == UserRole.CityUser)
            {
                return Unauthorized("You Don't have access.");
            }

            var result = await _kpiService.GetMutiplekpiLayerResults(request, userId.GetValueOrDefault(), userRole, userPlan);
            if (result == null)
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(result);
        }
    }
}
