using PeaceEnablers.Common.Models;
using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Dtos.AssessmentDto;
using PeaceEnablers.Dtos.CountryDto;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Dtos.kpiDto;
using PeaceEnablers.Dtos.PublicDto;
using PeaceEnablers.Enums;
using PeaceEnablers.Models;
using PeaceEnablers.Dtos.CountryUserDto;

namespace PeaceEnablers.IServices
{
    public interface ICountryUserService
    {
        Task<ResultResponseDto<List<PartnerCountryResponseDto>>> GetCountryUserCountries(int userID);
        Task<ResultResponseDto<CountryHistoryDto>> GetCountryHistory(int userId, TieredAccessPlan tier);
        Task<ResultResponseDto<List<GetCountriesSubmitionHistoryResponseDto>>> GetCountriesProgressByUserId(int userID);
        Task<GetCountryQuestionHistoryResponseDto> GetCountryQuestionHistory(UserCountryRequestDto userCountryRequstDto);
        Task<PaginationResponse<CountryResponseDto>> GetCountriesAsync(PaginationRequest request);
        Task<ResultResponseDto<CountryDetailsDto>> GetCountryDetails(UserCountryRequestDto userCountryRequstDto);
        Task<ResultResponseDto<List<CountryPillarQuestionDetailsDto>>> GetCountryPillarDetails(UserCountryGetPillarInfoRequestDto userCountryGetPillarInfoRequestDto);
        Task<ResultResponseDto<string>> AddCountryUserKpisCountryAndPillar(AddCountryUserKpisCountryAndPillar payload,int userID, string tierName);
        Task<ResultResponseDto<List<GetAllKpisResponseDto>>> GetCountryUserKpi(int userID, string tierName);
        Task<ResultResponseDto<CompareCountryResponseDto>> CompareCountries(CompareCountryRequestDto c, int userId, string tierName, bool applyPagination = true);
        Task<ResultResponseDto<AiCountryPillarResponseDto>> GetAICountryPillars(AiCountryPillarRequestDto r, int userID, string tierName);
        Task<Tuple<string, byte[]>> ExportCompareCountries(CompareCountryRequestDto request, int userId, string tierName);
    }
}
