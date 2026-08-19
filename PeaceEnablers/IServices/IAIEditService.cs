using PeaceEnablers.Common.Models;
using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Models;

namespace PeaceEnablers.IServices
{
    public interface IAIEditService
    {
        Task<ResultResponseDto<bool>> UpdateAICountryScore(UpdateAICountryScoreDto dto, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> UpdateAIPillarScore(UpdateAIPillarScoreDto dto, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> UpdateAIDataSourceCitation(UpdateAIDataSourceCitationDto dto, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> UpdateAIEstimatedQuestionScore(UpdateAIEstimatedQuestionScoreDto dto, int userID, UserRole userRole);

        Task<bool> CanAnalystEditAsync(int userID, int countryID, int year);
        Task<ResultResponseDto<AIEditAccessDto>> GetEditAccess(int userID, UserRole userRole, int countryID, int year);

        Task<ResultResponseDto<AIEditPermissionDto>> RequestPermission(RequestAIEditPermissionDto dto, int userID);
        Task<ResultResponseDto<AIEditPermissionDto>> GrantPermission(GrantAIEditPermissionDto dto, int adminUserID);
        Task<ResultResponseDto<bool>> ReviewPermissionRequest(ReviewAIEditPermissionDto dto, int adminUserID);
        Task<ResultResponseDto<bool>> RevokePermission(int permissionID, int adminUserID);
        Task<PaginationResponse<AIEditPermissionDto>> GetPermissions(AIEditPermissionListRequestDto request);

        Task<ResultResponseDto<AIEditSessionDto>> SubmitSession(int sessionID, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> ReviewSession(ReviewAIEditSessionDto dto, int adminUserID);
        Task<PaginationResponse<AIEditSessionDto>> GetSessions(AIEditSessionListRequestDto request);
        Task<ResultResponseDto<AIEditSessionDetailDto>> GetSessionDetail(int sessionID);
        Task<ResultResponseDto<List<AIEditChangeLogDto>>> GetChangeHistory(AIEditHistoryRequestDto request);

        /// <summary>
        /// Analyst path: write field diffs into the open draft session. Does not touch live AI tables.
        /// </summary>
        Task<ResultResponseDto<bool>> SaveAnalystDraftAsync(
            int userID,
            int countryID,
            int year,
            AIEditEntityType entityType,
            int entityRecordID,
            int? pillarID,
            int? questionID,
            IReadOnlyDictionary<string, (string? OldValue, string? NewValue)> fieldChanges);

        /// <summary>
        /// Admin path: already updated live — append published audit rows.
        /// </summary>
        Task LogAdminPublishAsync(
            int adminUserID,
            int countryID,
            int year,
            AIEditEntityType entityType,
            int entityRecordID,
            int? pillarID,
            int? questionID,
            IReadOnlyDictionary<string, (string? OldValue, string? NewValue)> fieldChanges);
    }
}
