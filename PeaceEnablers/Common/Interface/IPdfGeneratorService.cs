

using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Models;
using static PeaceEnablers.Services.AIComputationService;

namespace PeaceEnablers.Common.Interface
{
    public interface IPdfGeneratorService
    {
        Task<byte[]> GenerateCityDetailsPdf(AiCitySummeryDto city, List<AiCityPillarResponse> pillars, List<KpiChartItem> kpis, List<PeerCityHistoryReportDto> peerCity, UserRole userRole);
        Task<byte[]> GeneratePillarDetailsPdf(AiCityPillarResponse cityDetails, UserRole userRole);
        Task<byte[]> GenerateAllCitiesDetailsPdf(List<AiCitySummeryDto> cities, Dictionary<int, List<AiCityPillarResponse>> pillars, List<KpiChartItem> kpis, UserRole userRole);
    }
}
