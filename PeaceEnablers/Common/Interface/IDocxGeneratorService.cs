

using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Models;
using static PeaceEnablers.Services.AIComputationService;

namespace PeaceEnablers.Common.Interface
{
    /// <summary>
    /// Low-level Word document generation contract.
    /// Consumed by <see cref="DocumentGeneratorService"/>;
    /// controllers should depend on <see cref="IDocumentGeneratorService"/> instead.
    /// </summary>
    public interface IDocxGeneratorService
    {
        Task<byte[]> GenerateCityDetailsDocx(
            AiCitySummeryDto city,
            List<AiCityPillarResponse> pillars,
            List<KpiChartItem> kpis,
            List<PeerCityHistoryReportDto> peerCities,
            UserRole userRole);

        Task<byte[]> GeneratePillarDetailsDocx(
            AiCityPillarResponse pillarData,
            UserRole userRole);

        Task<byte[]> GenerateAllCitiesDetailsDocx(
            List<AiCitySummeryDto> cities,
            Dictionary<int, List<AiCityPillarResponse>> pillarsDict,
            List<KpiChartItem> kpis,
            UserRole userRole);
    }
}
