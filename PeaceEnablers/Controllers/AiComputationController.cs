using AssessmentPlatform.Dtos.AiDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Dtos.AssessmentDto;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;
using System.Security.Claims;

namespace PeaceEnablers.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AiComputationController : ControllerBase
    {

        private readonly IAIComputationService _aIComputationService;
        public AiComputationController(IAIComputationService aIComputationService)
        {
            _aIComputationService = aIComputationService;
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

        [HttpGet("getAITrustLevels")]
        public async Task<IActionResult> GetAITrustLevels()
        {
            return Ok(await _aIComputationService.GetAITrustLevels());
        }
        [HttpGet("getAICountries")]
        public async Task<IActionResult> GetAICountries([FromQuery] AiCountrySummeryRequestDto request)
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

            return Ok(await _aIComputationService.GetAICountries(request, userId.Value, userRole));
        }

        [HttpGet("getAICountryPillars")]
        public async Task<IActionResult> GetAICountryPillars([FromQuery] AiCountryPillarRequestDto request)
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

            return Ok(await _aIComputationService.GetAICountryPillars(request.CountryID, userId.Value, userRole, request.Year));
        }

        [HttpGet("getAIPillarQuestions")]
        public async Task<IActionResult> GetAIPillarQuestions([FromQuery] AiCountryPillarSummeryRequestDto r)
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

            return Ok(await _aIComputationService.GetAIPillarsQuestion(r, userId.Value, userRole));
        }

        [HttpGet("aiCountryDetailsReport")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DownloadCountryReport([FromQuery] AiCountrySummeryRequestPdfDto request)
        {
            try
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

                var countryDetails = await _aIComputationService.GetCountryAiSummeryDetail(userId ?? 0, userRole, request.CountryID,request.Year);

                // Generate PDF               

                string fileName;
                byte[] fileBytes;
                string contentType;

                fileBytes = await _aIComputationService.GenerateCountryDetailsReport(countryDetails, userRole, userId ?? 0, request.Format, request.ReportType);

                if (request.Format == IServices.DocumentFormat.Docx)
                {
                    contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                    fileName = $"{countryDetails.CountryName}_Details_{DateTime.Now:yyyyMMdd}.docx";
                }
                else
                {
                    contentType = "application/pdf";
                    fileName = $"{countryDetails.CountryName}_Details_{DateTime.Now:yyyyMMdd}.pdf";
                }

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                // Log error
                return StatusCode(500, new
                {
                    message = "Error generating report",
                    error = ex.Message
                });
            }
        }
        [HttpGet("aiPillarDetailsReport")]
        [Authorize(Policy = "StaffOnly")]
        public async Task<IActionResult> DownloadPillarReport([FromQuery] AiCountrySummeryRequestPdfDto request)
        {
            try
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
                if (userRole != UserRole.Admin && userRole != UserRole.CountryUser)
                    return Unauthorized("You Don't have access.");


                var pillars = await _aIComputationService.GetAICountryPillars(request.CountryID, userId.Value, userRole, request.Year);

                var pillarDetails = pillars.Result.Pillars.FirstOrDefault(x => x.PillarID == request.PillarID);
                if (pillarDetails != null)
                {
                    string contentType;
                    string fileName;

                    // Generate PDF
                    var fileBytes = await _aIComputationService.GeneratePillarDetailsReport(pillarDetails, userRole, request.Format);

                    if (request.Format == IServices.DocumentFormat.Docx)
                    {
                        contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                        fileName = $"{pillarDetails.PillarName}_Details_{DateTime.Now:yyyyMMdd}.docx";
                    }
                    else
                    {
                        contentType = "application/pdf";
                        fileName = $"{pillarDetails.PillarName}_Details_{DateTime.Now:yyyyMMdd}.pdf";
                    }

                    return File(fileBytes, contentType, fileName);
                }
                return StatusCode(500, new
                {
                    message = "Error generating Report"
                });

            }
            catch (Exception ex)
            {
                // Log error
                return StatusCode(500, new
                {
                    message = "Error generating Report",
                    error = ex.Message
                });
            }
        }

        [HttpPost("getAICrossCountryPillars")]
        [Authorize(Roles = "Admin, Analyst")]
        public async Task<IActionResult> GetAICrossCountryPillars([FromBody] AiCountryIdsDto aiCountryIdsDto)
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

            return Ok(await _aIComputationService.GetAICrossCountryPillars(aiCountryIdsDto, userId.Value, userRole));
        }

        [HttpPost("changedAiCountryEvaluationStatus")]
        [Authorize(Policy = "StaffOnly")]
        public async Task<IActionResult> ChangedAiCountryEvaluationStatus([FromBody] ChangedAiCountryEvaluationStatusDto aiCountryIdsDto)
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

            return Ok(await _aIComputationService.ChangedAiCountryEvaluationStatus(aiCountryIdsDto, userId.Value, userRole));
        }

        [HttpPost("regenerateAiSearch")]
        [Authorize(Roles = "Admin, Analyst")]

        public async Task<IActionResult> RegenerateAiSearch([FromBody] RegenerateAiSearchDto aiCountryIdsDto)
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

            return Ok(await _aIComputationService.RegenerateAiSearch(aiCountryIdsDto, userId.Value, userRole));
        }

        [HttpPost("addComment")]
        [Authorize(Policy = "StaffOnly")]
        public async Task<IActionResult> AddComment([FromBody] AddCommentDto aiCountryIdsDto)
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

            return Ok(await _aIComputationService.AddComment(aiCountryIdsDto, userId.Value, userRole));
        }
        [HttpPost("regeneratePillarAiSearch")]
        [Authorize(Roles = "Admin, Analyst")]
        public async Task<IActionResult> RegeneratePillarAiSearch([FromBody] RegeneratePillarAiSearchDto aiCountryIdsDto)
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

            return Ok(await _aIComputationService.RegeneratePillarAiSearch(aiCountryIdsDto, userId.Value, userRole));
        }
        [HttpGet("aiAllCountryDetailsReport")]
        public async Task<IActionResult> DownloadAllCountryPdf([FromQuery] DownloadReportDto request)
        {
            try
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
                var year = DateTime.Now.Year;
                var countryDetails = await _aIComputationService.GetAllCountryAiSummeryDetail(userId ?? 0, userRole, year);
                
                if (countryDetails.Count > 0)
                {
                    string fileName;
                    string contentType;
                    var pdfBytes = await _aIComputationService.GenerateAllCountryDetailsReport(countryDetails, userRole, userId.GetValueOrDefault(), year, request.Format);

                    if (request.Format == IServices.DocumentFormat.Docx)
                    {
                        contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                        fileName = $"Countries_Details_{DateTime.Now:yyyyMMdd}.docx";
                    }
                    else
                    {
                        contentType = "application/pdf";
                        fileName = $"Countries_Details_{DateTime.Now:yyyyMMdd}.pdf";
                    }

                    return File(pdfBytes, contentType, fileName);
                }

                return NotFound("No Country Found.");

            }
            catch (Exception ex)
            {
                // Log error
                return StatusCode(500, new
                {
                    message = "Error generating PDF",
                    error = ex.Message
                });
            }
        }
        [HttpPost("aiResultTransfer")]
        [Authorize(Roles = "Admin, Analyst")]
        public async Task<IActionResult> AiResultTransfer([FromBody] AITransferAssessmentRequestDto aiCountryIdsDto)
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

            return Ok(await _aIComputationService.AITransferAssessment(aiCountryIdsDto, userId.Value, userRole));
        }

        [HttpGet("reCalculateKpis")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> ReCalculateKpis()
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

            return Ok(await _aIComputationService.ReCalculateKpis(userId.Value, userRole));
        }

        [HttpPost("uploadAiDocuments")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> UploadAiDocuments([FromForm]  UploadAiDocumentRequest uploadAiDocumentRequest)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            if (uploadAiDocumentRequest.Files == null || !uploadAiDocumentRequest.Files.Any())
                return BadRequest("No files uploaded.");


            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _aIComputationService.UploadAiDocuments(uploadAiDocumentRequest ,userId.Value, userRole));
        }

        [HttpGet("getAICountryDocuments")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> GetAICountryDocuments([FromQuery] AiCountryDocumentRequestDto uploadAiDocumentRequest)
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

            return Ok(await _aIComputationService.GetAICountryDocuments(uploadAiDocumentRequest, userId.Value, userRole));
        }

        [HttpGet("getAICountryPillarDocuments")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> GetAICountryPillarDocuments([FromQuery] AiCountryPillarDocumentRequestDto uploadAiDocumentRequest)
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

            return Ok(await _aIComputationService.GetAICountryPillarDocuments(uploadAiDocumentRequest, userId.Value, userRole));
        }

        [HttpPost("deleteDocument")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> DeleteDocument([FromBody] DeleteCountryDocumentRequestDto uploadAiDocumentRequest)
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

            return Ok(await _aIComputationService.DeleteDocument(uploadAiDocumentRequest, userId.Value, userRole));
        }

        [HttpGet("downloadDocument/{Id}")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> DownloadDocument(int Id)
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

            var result = await _aIComputationService.DownloadDocument(Id, userId.GetValueOrDefault(), userRole);

            return result;
        }
    }
}
