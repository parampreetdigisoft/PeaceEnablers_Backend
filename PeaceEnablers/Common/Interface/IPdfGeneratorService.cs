

using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Models;
using static PeaceEnablers.Services.AIComputationService;

namespace PeaceEnablers.Common.Interface
{
    public interface IPdfGeneratorService
    {
        Task<byte[]> GenerateCountryDetailsPdf(AiCountrySummeryDto country, List<AiCountryPillarResponse> pillars, List<KpiChartItem> kpis, List<PeerCountryHistoryReportDto> peercountry, UserRole userRole);
        Task<byte[]> GeneratePillarDetailsPdf(AiCountryPillarResponse countryDetails, UserRole userRole);
        Task<byte[]> GenerateAllCountriesDetailsPdf(List<AiCountrySummeryDto> countries, Dictionary<int, List<AiCountryPillarResponse>> pillars, List<KpiChartItem> kpis, UserRole userRole);
    }
}
