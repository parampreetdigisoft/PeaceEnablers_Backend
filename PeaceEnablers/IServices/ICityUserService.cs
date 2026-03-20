using PeaceEnablers.Common.Models;
using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Dtos.AssessmentDto;
using PeaceEnablers.Dtos.CityDto;
using PeaceEnablers.Dtos.CityUserDto;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Dtos.kpiDto;
using PeaceEnablers.Dtos.PublicDto;
using PeaceEnablers.Enums;
using PeaceEnablers.Models;

namespace PeaceEnablers.IServices
{
    public interface ICityUserService
    {
        Task<ResultResponseDto<List<PartnerCityResponseDto>>> GetCityUserCities(int userID);
        Task<ResultResponseDto<CityHistoryDto>> GetCityHistory(int userId, TieredAccessPlan tier);
        Task<ResultResponseDto<List<GetCitiesSubmitionHistoryReponseDto>>> GetCitiesProgressByUserId(int userID);
        Task<GetCityQuestionHistoryReponseDto> GetCityQuestionHistory(UserCityRequstDto userCityRequstDto);
        Task<PaginationResponse<CityResponseDto>> GetCitiesAsync(PaginationRequest request);
        Task<ResultResponseDto<CityDetailsDto>> GetCityDetails(UserCityRequstDto userCityRequstDto);
        Task<ResultResponseDto<List<CityPillarQuestionDetailsDto>>> GetCityPillarDetails(UserCityGetPillarInfoRequstDto userCityRequstDto);
        Task<ResultResponseDto<string>> AddCityUserKpisCityAndPillar(AddCityUserKpisCityAndPillar payload,int userID, string tierName);
        Task<ResultResponseDto<List<GetAllKpisResponseDto>>> GetCityUserKpi(int userID, string tierName);
        Task<ResultResponseDto<CompareCityResponseDto>> CompareCities(CompareCityRequestDto c, int userId, string tierName, bool applyPagination = true);
        Task<ResultResponseDto<AiCityPillarReponseDto>> GetAICityPillars(AiCityPillarRequestDto r, int userID, string tierName);
        Task<Tuple<string, byte[]>> ExportCompareCities(CompareCityRequestDto request, int userId, string tierName);
    }
}
