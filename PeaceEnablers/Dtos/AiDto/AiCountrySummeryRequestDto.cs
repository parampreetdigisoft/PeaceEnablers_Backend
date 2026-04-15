using PeaceEnablers.Dtos.CommonDto;

namespace PeaceEnablers.Dtos.AiDto
{
    public class AiCountrySummeryRequestDto : PaginationRequest
    {
        public int? CountryID { get; set; }
        public int Year { get; set; } = DateTime.UtcNow.Year;
    }

    public class AiCountryPillarSummeryRequestDto : AiCountrySummeryRequestDto
    {
        public int? PillarID { get; set; }
    }

    public class AiCountrySummeryRequestPdfDto : AiCountryPillarRequestDto
    {
        public int? PillarID { get; set; }
        public PeaceEnablers.IServices.DocumentFormat Format { get; set; } = PeaceEnablers.IServices.DocumentFormat.Pdf;
        public string ReportType { get; set; } = "ai";
    }
    public class AiCountryPillarRequestDto
    {
        public int CountryID { get; set; }
        public int Year { get; set; } = DateTime.UtcNow.Year;
    }
    public class AiCountryDocumentRequestDto : PaginationRequest
    {
        public int? CountryID { get; set; }
    }

    public class AiCountryPillarDocumentRequestDto 
    {
        public int CountryID { get; set; }
    }
    public class DeleteCountryDocumentRequestDto 
    {
        public int CountryID { get; set; }
        public int? CountryDocumentID { get; set; }
        public bool IsAll { get; set; } = false;
    }

}
