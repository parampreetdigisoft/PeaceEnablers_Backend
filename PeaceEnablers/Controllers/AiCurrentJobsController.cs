using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.IServices;

namespace PeaceEnablers.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AiCurrentJobsController : ControllerBase
    {
        private readonly IAiCurrentJobsService _aiCurrentJobsService;

        public AiCurrentJobsController(IAiCurrentJobsService aiCurrentJobsService)
        {
            _aiCurrentJobsService = aiCurrentJobsService;
        }

        [HttpGet("health")]
        public async Task<IActionResult> GetHealth()
        {
            return Ok(await _aiCurrentJobsService.GetHealthAsync());
        }

        [HttpGet("jobs")]
        public async Task<IActionResult> GetJobs([FromQuery] AiCurrentJobsFilterDto filter)
        {
            return Ok(await _aiCurrentJobsService.GetJobsAsync(filter ?? new AiCurrentJobsFilterDto()));
        }

        [HttpGet("jobs/{jobId}")]
        public async Task<IActionResult> GetJob(string jobId)
        {
            return Ok(await _aiCurrentJobsService.GetJobAsync(jobId));
        }

        [HttpGet("countries/{countryId}/status")]
        public async Task<IActionResult> GetCountryStatus(int countryId)
        {
            return Ok(await _aiCurrentJobsService.GetCountryStatusAsync(countryId));
        }

        [HttpPost("jobs/{jobId}/cancel")]
        public async Task<IActionResult> CancelJob(string jobId)
        {
            return Ok(await _aiCurrentJobsService.CancelJobAsync(jobId));
        }
    }
}
