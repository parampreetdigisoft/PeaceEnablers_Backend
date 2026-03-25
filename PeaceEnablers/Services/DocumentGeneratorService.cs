
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

        public Task<byte[]> GenerateCityDetails(
            AiCitySummeryDto city,
            List<AiCityPillarResponse> pillars,
            List<KpiChartItem> kpis,
            List<PeerCityHistoryReportDto> peerCity,
            UserRole userRole,
        PeaceEnablers.IServices.DocumentFormat format = PeaceEnablers.IServices.DocumentFormat.Pdf)
        {
             var result = format == PeaceEnablers.IServices.DocumentFormat.Docx
                ? _docx.GenerateCityDetailsDocx(city, pillars, kpis, peerCity, userRole)
                : _pdf.GenerateCityDetailsPdf(city, pillars, kpis, peerCity, userRole);

            return result;
        }

        public Task<byte[]> GeneratePillarDetails(
            AiCityPillarResponse pillarData,
            UserRole userRole,
            PeaceEnablers.IServices.DocumentFormat format = PeaceEnablers.IServices.DocumentFormat.Pdf)
            => format == PeaceEnablers.IServices.DocumentFormat.Docx
                ? _docx.GeneratePillarDetailsDocx(pillarData, userRole)
                : _pdf.GeneratePillarDetailsPdf(pillarData, userRole);

        public Task<byte[]> GenerateAllCitiesDetails(
            List<AiCitySummeryDto> cities,
            Dictionary<int, List<AiCityPillarResponse>> pillarsDict,
            List<KpiChartItem> kpis,
            UserRole userRole,
            PeaceEnablers.IServices.DocumentFormat format = PeaceEnablers.IServices.DocumentFormat.Pdf)
            => format == PeaceEnablers.IServices.DocumentFormat.Docx
                ? _docx.GenerateAllCitiesDetailsDocx(cities, pillarsDict, kpis, userRole)
                : _pdf.GenerateAllCitiesDetailsPdf(cities, pillarsDict, kpis, userRole);
    }
}
