using PeaceEnablers.Models;

namespace PeaceEnablers.Dtos.CountryDto
{
    public class CountryResponseDto : Country
    {
        public string? AssignedBy { get; set; }
        public decimal? Score { get; set; }// highest score have top rank
        public decimal? AiScore { get; set; }
    }
    public class UserCountryMappingResponseDto : CountryResponseDto
    {
        public int UserCountryMappingID { get; set; }
        public double? Distance { get; set; }
        public List<int>? PeerCountryIDs { get; set; }
    }
}
