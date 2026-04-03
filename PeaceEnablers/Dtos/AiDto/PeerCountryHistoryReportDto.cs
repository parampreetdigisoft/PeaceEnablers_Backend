namespace PeaceEnablers.Dtos.AiDto
{
    public class PeerCountryHistoryReportDto
    {
        public int CountryID { get; set; }
        public string Continent { get; set; }
        public string CountryName { get; set; }
        public string? CountryCode { get; set; }
        public string? Region { get; set; }
        public string Country { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string? Image { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int? Population { get; set; }
        public decimal? Income { get; set; }       
        public List<PeerCountryYearHistoryDto> CountryHistory { get; set; }
    }

    public class PeerCountryYearHistoryDto
    {
        public int CountryID { get; set; }
        public int Year { get; set; } = 0;
        public decimal ScoreProgress { get; set; }
        public List<PeerCountryPillarHistoryReportDto> Pillars { get; set; }
    }

    public class PeerCountryPillarHistoryReportDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public int DisplayOrder { get; set; } = 0;
        public decimal ScoreProgress { get; set; }

    }
}
