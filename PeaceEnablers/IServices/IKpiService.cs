using PeaceEnablers.Common.Models;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Dtos.CountryUserDto;
using PeaceEnablers.Dtos.kpiDto;
using PeaceEnablers.Enums;
using PeaceEnablers.Models;

namespace PeaceEnablers.IServices
{
    public interface IKpiService
    {
        Task<PaginationResponse<GetAnalyticalLayerResultDto>> GetAnalyticalLayerResults(GetAnalyticalLayerRequestDto request, int userId, UserRole role, TieredAccessPlan userPlan = TieredAccessPlan.Pending);
        Task<ResultResponseDto<List<AnalyticalLayer>>> GetAllKpi(int userId, UserRole role);
        Task<ResultResponseDto<CompareCountryResponseDto>> CompareCountries(CompareCountryRequestDto c, int userId, UserRole role, bool applyPagination = true);

        Task<Tuple<string, byte[]>> ExportCompareCountries(CompareCountryRequestDto request, int userId, UserRole role);
        Task<ResultResponseDto<GetMutiplekpiLayerResultsDto>> GetMutiplekpiLayerResults(GetMutiplekpiLayerRequestDto request, int userId, UserRole role, TieredAccessPlan userPlan = TieredAccessPlan.Pending);
    }
}
