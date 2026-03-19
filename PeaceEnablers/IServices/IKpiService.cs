using PeaceEnablers.Common.Models;
using PeaceEnablers.Dtos.CityUserDto;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Dtos.kpiDto;
using PeaceEnablers.Enums;
using PeaceEnablers.Models;

namespace PeaceEnablers.IServices
{
    public interface IKpiService
    {
        Task<PaginationResponse<GetAnalyticalLayerResultDto>> GetAnalyticalLayerResults(GetAnalyticalLayerRequestDto request, int userId, UserRole role, TieredAccessPlan userPlan = TieredAccessPlan.Pending);
        Task<ResultResponseDto<List<AnalyticalLayer>>> GetAllKpi();
        Task<ResultResponseDto<CompareCityResponseDto>> CompareCities(CompareCityRequestDto c, int userId, UserRole role, bool applyPagination = true);

        Task<Tuple<string, byte[]>> ExportCompareCities(CompareCityRequestDto request, int userId, UserRole role);
        Task<ResultResponseDto<GetMutiplekpiLayerResultsDto>> GetMutiplekpiLayerResults(GetMutiplekpiLayerRequestDto request, int userId, UserRole role, TieredAccessPlan userPlan = TieredAccessPlan.Pending);
    }
}
