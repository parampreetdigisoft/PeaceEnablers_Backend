using PeaceEnablers.Common.Models;
using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Dtos.CityDto;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Models;

namespace PeaceEnablers.IServices
{
    public interface IAIComputationService
    {
        Task<ResultResponseDto<List<AITrustLevel>>> GetAITrustLevels();
        Task<PaginationResponse<AiCitySummeryDto>> GetAICities(AiCitySummeryRequestDto request, int userID, UserRole userRole);
        Task<ResultResponseDto<AiCityPillarReponseDto>> GetAICityPillars(int cityID, int userID, UserRole userRole,int year=0);
        Task<PaginationResponse<AIEstimatedQuestionScoreDto>> GetAIPillarsQuestion(AiCityPillarSummeryRequestDto r, int userID, UserRole userRole);
        Task<IQueryable<AiCitySummeryDto>> GetCityAiSummeryDetails(int userID, UserRole userRole, int? cityID, int year=0);
        Task<byte[]> GenerateCityDetailsPdf(AiCitySummeryDto cityDetails, UserRole userRole, int userID);
        Task<byte[]> GeneratePillarDetailsPdf(AiCityPillarResponse cityDetails, UserRole userRole);
        Task<ResultResponseDto<AiCrossCityResponseDto>> GetAICrossCityPillars(AiCityIdsDto ids, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> ChangedAiCityEvaluationStatus(ChangedAiCityEvaluationStatusDto aiCityIdsDto, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> RegenerateAiSearch(RegenerateAiSearchDto aiCityIdsDto, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> AddComment(AddCommentDto aiCityIdsDto, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> RegeneratePillarAiSearch(RegeneratePillarAiSearchDto aiCityIdsDto, int userID, UserRole userRole);
        Task<AiCitySummeryDto> GetCityAiSummeryDetail(int userID, UserRole userRole, int? cityID, int year);
        Task<List<AiCitySummeryDto>> GetAllCityAiSummeryDetail(int userID, UserRole userRole, int year);
        Task<byte[]> GenerateAllCityDetailsPdf(List<AiCitySummeryDto> cityDetails, UserRole userRole, int userID, int year);
    }
}
