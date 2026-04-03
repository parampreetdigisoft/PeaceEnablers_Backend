using PeaceEnablers.Common.Models;
using PeaceEnablers.Dtos.AssessmentDto;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Dtos.QuestionDto;
using PeaceEnablers.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PeaceEnablers.IServices
{
    public interface IQuestionService
    {
        Task<List<Pillar>> GetPillarsAsync();
        Task<PaginationResponse<GetQuestionResponse>> GetQuestionsAsync(GetQuestionRequestDto requestDto);
        Task<Question> AddQuestionAsync(Question q);
        Task<ResultResponseDto<string>> AddUpdateQuestion(AddUpdateQuestionDto q);
        Task<ResultResponseDto<string>> AddBulkQuestion(AddBulkQuestionsDto q);
        Task<Question> EditQuestionAsync(int id, Question q);
        Task<bool> DeleteQuestionAsync(int id);
        Task<ResultResponseDto<GetPillarQuestionByCountryResponse>> GetQuestionsByCountryIdAsync(CountryPillerRequestDto request, int userId);
        Task<Tuple<string,byte[]>> ExportAssessment(int userCountryMappingID);
        Task<ResultResponseDto<List<QuestionsByUserPillarsResponsetDto>>> GetQuestionsHistoryByPillar(GetCountryPillarHistoryRequestDto requestDto);
        Task<ResultResponseDto<GetPillarQuestionByCountryResponse>> GetQuestionsByCountryMappingIdForAnalyst(CountryPillerRequestDto request, int userId);
    }
} 