using PeaceEnablers.Dtos.CommonDto;

namespace PeaceEnablers.Dtos.AiDto
{
    public class AiCitySummeryRequestDto : PaginationRequest
    {
        public int? CityID { get; set; }
        public int Year { get; set; } = DateTime.UtcNow.Year;
    }

    public class AiCityPillarSummeryRequestDto : AiCitySummeryRequestDto
    {
        public int? PillarID { get; set; }
    }

    public class AiCitySummeryRequestPdfDto : AiCityPillarRequestDto
    {
        public int? PillarID { get; set; }
        public PeaceEnablers.IServices.DocumentFormat Format { get; set; } = PeaceEnablers.IServices.DocumentFormat.Pdf;
        public string ReportType { get; set; } = "ai";
    }
    public class AiCityPillarRequestDto
    {
        public int CityID { get; set; }
        public int Year { get; set; } = DateTime.UtcNow.Year;
    }
}
