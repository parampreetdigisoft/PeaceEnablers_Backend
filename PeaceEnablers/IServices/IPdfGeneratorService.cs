using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Models;

using static PeaceEnablers.Services.AIComputationService;

namespace PeaceEnablers.IServices
{
    public interface IPdfGeneratorService
    {
        Task<byte[]> GenerateCityDetailsPdf(AiCitySummeryDto city, List<AiCityPillarResponse> pillars, List<KpiChartItem> kpis, UserRole userRole);
        Task<byte[]> GeneratePillarDetailsPdf(AiCityPillarResponse cityDetails, UserRole userRole);
        Task<byte[]> GenerateAllCitiesDetailsPdf(List<AiCitySummeryDto> cities, Dictionary<int, List<AiCityPillarResponse>> pillars, List<KpiChartItem> kpis, UserRole userRole);
    }
}
