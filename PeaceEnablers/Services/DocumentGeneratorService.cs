
using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.Models;
using PeaceEnablers.Common.Interface;
using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;
using static PeaceEnablers.Services.AIComputationService;


namespace PeaceEnablers.Services
{
    /// <summary>
    /// Facade that delegates to <see cref="PdfGeneratorService"/> or
    /// <see cref="DocxGeneratorService"/> based on the requested <see cref="DocumentFormat"/>.
    ///
    /// Register as: services.AddScoped&lt;IDocumentGeneratorService, DocumentGeneratorService&gt;()
    /// </summary>
    public sealed class DocumentGeneratorService : IDocumentGeneratorService
    {
        private readonly Common.Interface.IPdfGeneratorService _pdf;
        private readonly IDocxGeneratorService _docx;

        public DocumentGeneratorService(
            Common.Interface.IPdfGeneratorService pdf,
            IDocxGeneratorService docx)
        {
            _pdf = pdf;
            _docx = docx;
        }

        public Task<byte[]> GenerateCountryDetails(
            AiCountrySummeryDto country,
            List<AiCountryPillarResponse> pillars,
            List<KpiChartItem> kpis,
            List<PeerCountryHistoryReportDto> peercountry,
            UserRole userRole,
        PeaceEnablers.IServices.DocumentFormat format = PeaceEnablers.IServices.DocumentFormat.Pdf)
        {
             var result = format == PeaceEnablers.IServices.DocumentFormat.Docx
                ? _docx.GenerateCountryDetailsDocx(country, pillars, kpis, peercountry, userRole)
                : _pdf.GenerateCountryDetailsPdf(country, pillars, kpis, peercountry, userRole);

            return result;
        }

        public Task<byte[]> GeneratePillarDetails(
            AiCountryPillarResponse pillarData,
            UserRole userRole,
            PeaceEnablers.IServices.DocumentFormat format = PeaceEnablers.IServices.DocumentFormat.Pdf)
            => format == PeaceEnablers.IServices.DocumentFormat.Docx
                ? _docx.GeneratePillarDetailsDocx(pillarData, userRole)
                : _pdf.GeneratePillarDetailsPdf(pillarData, userRole);

        public Task<byte[]> GenerateAllCountriesDetails(
            List<AiCountrySummeryDto> countries,
            Dictionary<int, List<AiCountryPillarResponse>> pillarsDict,
            List<KpiChartItem> kpis,
            UserRole userRole,
            PeaceEnablers.IServices.DocumentFormat format = PeaceEnablers.IServices.DocumentFormat.Pdf)
            => format == PeaceEnablers.IServices.DocumentFormat.Docx
                ? _docx.GenerateAllCountriesDetailsDocx(countries, pillarsDict, kpis, userRole)
                : _pdf.GenerateAllCountriesDetailsPdf(countries, pillarsDict, kpis, userRole);
    }
}
