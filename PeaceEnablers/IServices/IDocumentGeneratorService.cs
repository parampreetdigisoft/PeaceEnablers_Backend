
using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Models;
using static PeaceEnablers.Services.AIComputationService;

namespace PeaceEnablers.IServices
{
    /// <summary>
    /// Output format for document generation.
    /// PDF is the default; Docx produces an editable Word document.
    /// </summary>
    public enum DocumentFormat
    {
        Pdf,
        Docx
    }

    /// <summary>
    /// Unified document-generation service.
    /// Replaces direct calls to IPdfGeneratorService.
    /// Pass <see cref="DocumentFormat.Docx"/> to get a Word document instead of a PDF.
    /// </summary>
    public interface IDocumentGeneratorService
    {
        /// <summary>Full country report: dashboard, summary, pillars, peer comparison, trends, KPI dashboard.</summary>
        Task<byte[]> GenerateCountryDetails(
            AiCountrySummeryDto country,
            List<AiCountryPillarResponse> pillars,
            List<KpiChartItem> kpis,
            List<PeerCountryHistoryReportDto> peerCountry,
            UserRole userRole,
            DocumentFormat format = DocumentFormat.Pdf);

        /// <summary>Single pillar detail report.</summary>
        Task<byte[]> GeneratePillarDetails(
            AiCountryPillarResponse pillarData,
            UserRole userRole,
            DocumentFormat format = DocumentFormat.Pdf);

        /// <summary>Combined report covering every country in the list.</summary>
        Task<byte[]> GenerateAllCountriesDetails(
            List<AiCountrySummeryDto> countries,
            Dictionary<int, List<AiCountryPillarResponse>> pillarsDict,
            List<KpiChartItem> kpis,
            UserRole userRole,
            DocumentFormat format = DocumentFormat.Pdf);
    }
}
