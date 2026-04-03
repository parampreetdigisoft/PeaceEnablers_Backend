using PeaceEnablers.Dtos.PublicDto;
using PeaceEnablers.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PeaceEnablers.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class PublicController : ControllerBase
    {
        public readonly IPublicService _publicService;
        public PublicController(IPublicService publicService)
        {
            _publicService = publicService;
        }

        [HttpGet("getAllCountries")]
        public async Task<IActionResult> getAllCountries()
        {
            var response = await _publicService.getAllCountries();
            return Ok(response);
        }

        [HttpGet("GetPartnerCountriesFilterRecord")]
        public async Task<IActionResult> GetPartnerCountriesFilterRecord() => Ok(await _publicService.GetPartnerCountriesFilterRecord());

        [HttpGet]
        [Route("GetAllPillarAsync")]
        public async Task<IActionResult> GetAllPillarAsync() => Ok(await _publicService.GetAllPillarAsync());

        [HttpGet("GetPartnerCountries")]
        public async Task<IActionResult> GetPartnerCountries([FromQuery] PartnerCountryRequestDto r)
        {
            var response = await _publicService.GetPartnerCountries(r);
            return Ok(response);
        }
        [HttpGet("DownloadExecutiveSummeryPdf")]
        public IActionResult DownloadExecutiveSummeryPdf()
        {
            try
            {
                var fileName = "Executive-Summary.pdf";
                // Assuming PDFs are in wwwroot/pdf folder
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "pdf", fileName);

                if (!System.IO.File.Exists(filePath))
                    return NotFound("File not found");

                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                return File(fileBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error");
            }
        }
        [HttpGet("DownloadSummeryReportPdf")]
        public IActionResult DownloadSummeryReportPdf()
        {
            try
            {
                var fileName = "download-summary-report.pdf";
                // Assuming PDFs are in wwwroot/pdf folder
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "pdf", fileName);

                if (!System.IO.File.Exists(filePath))
                    return NotFound("File not found");

                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                return File(fileBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error");
            }
        }
        [HttpGet("countries-Countries")]
        public async Task<IActionResult> GetCountriesCountries()
        {
            var data = await _publicService.GetCountriesAndCountries_WithStaleSupport();
            return Ok(data);
        }

        [HttpGet("promoted-Countries")]
        public async Task<IActionResult> GetPromotedCountries()
        {
            var data = await _publicService.GetPromotedCountries();
            return Ok(data);
        }

    }
}
