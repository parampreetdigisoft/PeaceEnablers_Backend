using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace PeaceEnablers.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AiEditController : ControllerBase
    {
        private readonly IAIEditService _auditService;

        public AiEditController(IAIEditService auditService)
        {
            _auditService = auditService;
        }

        private int? GetUserIdFromClaims()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }

        private string? GetRoleFromClaims() => User.FindFirst(ClaimTypes.Role)?.Value;

        private bool TryGetUser(out int userId, out UserRole userRole, out IActionResult? error)
        {
            userId = 0;
            userRole = UserRole.Admin;
            error = null;

            var id = GetUserIdFromClaims();
            if (id == null)
            {
                error = Unauthorized("User ID not found in token.");
                return false;
            }

            var role = GetRoleFromClaims();
            if (role == null || !Enum.TryParse<UserRole>(role, true, out userRole))
            {
                error = Unauthorized("You Don't have access.");
                return false;
            }

            userId = id.Value;
            return true;
        }
        
        [HttpPost("updateAICountryScore")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> UpdateAICountryScore([FromBody] UpdateAICountryScoreDto request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null || !Enum.TryParse<UserRole>(role, true, out var userRole))
                return Unauthorized("You Don't have access.");

            return Ok(await _auditService.UpdateAICountryScore(request, userId.Value, userRole));
        }

        [HttpPost("updateAIPillarScore")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> UpdateAIPillarScore([FromBody] UpdateAIPillarScoreDto request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null || !Enum.TryParse<UserRole>(role, true, out var userRole))
                return Unauthorized("You Don't have access.");

            return Ok(await _auditService.UpdateAIPillarScore(request, userId.Value, userRole));
        }

        [HttpPost("updateAIDataSourceCitation")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> UpdateAIDataSourceCitation([FromBody] UpdateAIDataSourceCitationDto request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null || !Enum.TryParse<UserRole>(role, true, out var userRole))
                return Unauthorized("You Don't have access.");

            return Ok(await _auditService.UpdateAIDataSourceCitation(request, userId.Value, userRole));
        }

        [HttpPost("updateAIEstimatedQuestionScore")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> UpdateAIEstimatedQuestionScore([FromBody] UpdateAIEstimatedQuestionScoreDto request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null || !Enum.TryParse<UserRole>(role, true, out var userRole))
                return Unauthorized("You Don't have access.");

            return Ok(await _auditService.UpdateAIEstimatedQuestionScore(request, userId.Value, userRole));
        }



        [HttpGet("getEditAccess")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> GetEditAccess([FromQuery] int countryID, [FromQuery] int year)
        {
            if (!TryGetUser(out var userId, out var userRole, out var error))
                return error!;
            return Ok(await _auditService.GetEditAccess(userId, userRole, countryID, year));
        }

        [HttpPost("requestPermission")]
        [Authorize(Roles = "Analyst")]
        public async Task<IActionResult> RequestPermission([FromBody] RequestAIEditPermissionDto request)
        {
            if (!TryGetUser(out var userId, out _, out var error))
                return error!;
            return Ok(await _auditService.RequestPermission(request, userId));
        }

        [HttpPost("grantPermission")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GrantPermission([FromBody] GrantAIEditPermissionDto request)
        {
            if (!TryGetUser(out var userId, out _, out var error))
                return error!;
            return Ok(await _auditService.GrantPermission(request, userId));
        }

        [HttpPost("reviewPermission")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReviewPermission([FromBody] ReviewAIEditPermissionDto request)
        {
            if (!TryGetUser(out var userId, out _, out var error))
                return error!;
            return Ok(await _auditService.ReviewPermissionRequest(request, userId));
        }

        [HttpPost("revokePermission/{permissionID:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RevokePermission(int permissionID)
        {
            if (!TryGetUser(out var userId, out _, out var error))
                return error!;
            return Ok(await _auditService.RevokePermission(permissionID, userId));
        }

        [HttpGet("permissions")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPermissions([FromQuery] AIEditPermissionListRequestDto request)
        {
            return Ok(await _auditService.GetPermissions(request));
        }

        [HttpGet("sessions")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> GetSessions([FromQuery] AIEditSessionListRequestDto request)
        {
            if (!TryGetUser(out var userId, out var userRole, out var error))
                return error!;

            // Analysts only see their own sessions
            if (userRole == UserRole.Analyst)
                request.UserId = userId;

            return Ok(await _auditService.GetSessions(request));
        }

        [HttpGet("session/{sessionID:int}")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> GetSessionDetail(int sessionID)
        {
            return Ok(await _auditService.GetSessionDetail(sessionID));
        }

        [HttpPost("submitSession/{sessionID:int}")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> SubmitSession(int sessionID)
        {
            if (!TryGetUser(out var userId, out var userRole, out var error))
                return error!;
            return Ok(await _auditService.SubmitSession(sessionID, userId, userRole));
        }

        [HttpPost("reviewSession")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReviewSession([FromBody] ReviewAIEditSessionDto request)
        {
            if (!TryGetUser(out var userId, out _, out var error))
                return error!;
            return Ok(await _auditService.ReviewSession(request, userId));
        }

        [HttpGet("history")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetHistory([FromQuery] AIEditHistoryRequestDto request)
        {
            return Ok(await _auditService.GetChangeHistory(request));
        }
    }
}
