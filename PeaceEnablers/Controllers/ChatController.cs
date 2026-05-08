using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Dtos.chatDto;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;
using PeaceEnablers.Services;
using System.Security.Claims;

namespace PeaceEnablers.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
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


        [HttpGet("getAssistantFAQDs")]
        public async Task<IActionResult> GetById(int id)
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

            var resp = await _chatService.GetAssistantFAQDs(userId.GetValueOrDefault(), userRole);
            if (resp == null) return NotFound();
            return Ok(resp);
        }

        [HttpPost("askAboutCountry")]
        public async Task<IActionResult> AskAboutCountry([FromBody] CountryChatRequestDto request)
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

            return Ok(await _chatService.AskAboutCountry(request));
        }

        [HttpPost("askGlobalQuestion")]
        public async Task<IActionResult> AskGlobalQuestion([FromBody] ChatGlobalAskQuestionRequestDto request)
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

            return Ok(await _chatService.AskAboutGlobal(request));
        }

    }
}
