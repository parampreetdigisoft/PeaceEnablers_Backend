namespace PeaceEnablers.Dtos.AiDto
{
    public class DownloadReportDto
    {
        public List<int>? CityIDs { get; set; }
        public IServices.DocumentFormat Format { get; set; } = IServices.DocumentFormat.Pdf;

    }
}
