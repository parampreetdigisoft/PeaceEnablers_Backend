using PeaceEnablers.Dtos.UserDtos;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PeaceEnablers.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest req)
        {

            return Created($"", new() { });
        }
        private int? GetUserIdFromClaims()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;

            return null;
        }
        [HttpGet]
        [Route("GetUserByRoleWithAssignedCountry")]
        public async Task<IActionResult> GetUserByRoleWithAssignedCountry([FromQuery] GetUserByRoleRequestDto request)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null || claimUserId != request.UserID)
                return Unauthorized("User ID not found.");

            return Ok(await _userService.GetUserByRoleWithAssignedCountry(request));
        }

        [HttpGet]
        [Route("GetEvaluatorByAnalyst")]
        public async Task<IActionResult> GetEvaluatorByAnalyst([FromQuery] GetAssignUserDto request)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null || claimUserId != request.UserID)
                return Unauthorized("User ID not found.");

            return Ok(await _userService.GetEvaluatorByAnalyst(request));
        }       

        [HttpGet]
        [Route("getUserInfo")]
        public async Task<IActionResult> getUserInfo()
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null )
                return Unauthorized("User ID not found.");

            return Ok(await _userService.GetUserInfo(claimUserId.GetValueOrDefault()));
        }


        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        [Route("getUsersAssignedToCountry/{countryID}")]
        public async Task<IActionResult> GetUsersAssignedToCountry(int countryID) => Ok(await _userService.GetUsersAssignedToCountry(countryID));
    }

    public class RegisterRequest
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Password { get; set; }
        public Enums.TieredAccessPlan? Tier { get; set; } = Enums.TieredAccessPlan.Pending;
        public UserRole Role { get; set; }
    }
} 