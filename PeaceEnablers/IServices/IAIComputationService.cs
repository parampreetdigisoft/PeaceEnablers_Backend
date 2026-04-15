using AssessmentPlatform.Dtos.AiDto;
using Microsoft.AspNetCore.Mvc;
using PeaceEnablers.Common.Models;
using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Models;

namespace PeaceEnablers.IServices
{
    public interface IAIComputationService
    {
        Task<ResultResponseDto<List<AITrustLevel>>> GetAITrustLevels();
        Task<PaginationResponse<AiCountrySummeryDto>> GetAICountries(AiCountrySummeryRequestDto request, int userID, UserRole userRole);
        Task<ResultResponseDto<AiCountryPillarResponseDto>> GetAICountryPillars(int countryID, int userID, UserRole userRole,int year=0);
        Task<PaginationResponse<AIEstimatedQuestionScoreDto>> GetAIPillarsQuestion(AiCountryPillarSummeryRequestDto r, int userID, UserRole userRole);
        Task<IQueryable<AiCountrySummeryDto>> GetCountryAiSummeryDetails(int userID, UserRole userRole, int? countryID, int year=0);
        Task<byte[]> GenerateCountryDetailsReport(AiCountrySummeryDto countriesDetails, UserRole userRole, int userID, DocumentFormat format = DocumentFormat.Pdf, string reportType = "AI");
        Task<byte[]> GeneratePillarDetailsReport(AiCountryPillarResponse countriesDetails, UserRole userRole,DocumentFormat format = DocumentFormat.Pdf);
        Task<ResultResponseDto<AiCrossCountryResponseDto>> GetAICrossCountryPillars(AiCountryIdsDto ids, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> ChangedAiCountryEvaluationStatus(ChangedAiCountryEvaluationStatusDto aiCountryIdsDto, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> RegenerateAiSearch(RegenerateAiSearchDto aiCountryIdsDto, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> AddComment(AddCommentDto aiCountryIdsDto, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> RegeneratePillarAiSearch(RegeneratePillarAiSearchDto aiCountryIdsDto, int userID, UserRole userRole);
        Task<AiCountrySummeryDto> GetCountryAiSummeryDetail(int userID, UserRole userRole, int? countryID, int year);
        Task<List<AiCountrySummeryDto>> GetAllCountryAiSummeryDetail(int userID, UserRole userRole, int year);   
        Task<byte[]> GenerateAllCountryDetailsReport(List<AiCountrySummeryDto> countriesDetails, UserRole userRole, int userID, int year, DocumentFormat format = DocumentFormat.Pdf);
        Task<ResultResponseDto<string>> AITransferAssessment(AITransferAssessmentRequestDto r, int userID, UserRole userRole);
        Task<ResultResponseDto<string>> ReCalculateKpis(int userID, UserRole userRole);
        Task<ResultResponseDto<string>> UploadAiDocuments(UploadAiDocumentRequest r, int userID, UserRole userRole);
        Task<PaginationResponse<GetCountryDocumentResponseDto>> GetAICountryDocuments(AiCountryDocumentRequestDto request,int userID, UserRole userRole);
        Task<ResultResponseDto<List<GetCountryPillarDocumentResponseDto>>> GetAICountryPillarDocuments(AiCountryPillarDocumentRequestDto request,int userID, UserRole userRole);
        Task<ResultResponseDto<string>> DeleteDocument(DeleteCountryDocumentRequestDto request, int userID, UserRole userRole);
        Task<FileResult> DownloadDocument(int countryDocumentID, int userID, UserRole userRole);
    }
}
