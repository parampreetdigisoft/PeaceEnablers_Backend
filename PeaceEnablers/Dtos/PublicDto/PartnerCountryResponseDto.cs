

namespace PeaceEnablers.Dtos.PublicDto
{
    public class PartnerCountryResponseDto : PartnerCountryHistoryResponseDto
    {
        public int CountryID { get; set; }
        public string Continent { get; set; }
        public string State { get; set; }
        public string CountryName { get; set; }
        public string? CountryCode { get; set; }
        public string? Region { get; set; }
        public string? Image { get; set; }
    }

    public class PartnerCountryHistoryResponseDto
    {
        public decimal Score { get; set; }
        public decimal HighScore { get; set; }
        public decimal LowerScore { get; set; }
        public decimal Progress { get; set; }
        public decimal AiScore { get; set; }

    }
}
