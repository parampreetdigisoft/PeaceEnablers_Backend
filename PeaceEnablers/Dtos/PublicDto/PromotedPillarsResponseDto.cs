namespace PeaceEnablers.Dtos.PublicDto
{
    public class PromotedPillarsResponseDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public string ImagePath { get; set; }
        public int DisplayOrder { get; set; }
        public List<PromotedCountryResponseDto> Countries { get; set; }
    }

    public class PromotedCountryResponseDto
    {
        public int CountryID { get; set; }        
        public string Continent { get; set; }
        public string CountryName { get; set; }
        public string? CountryCode { get; set; }
        public string? Region { get; set; }
        public string? Image { get; set; }
        public decimal? ScoreProgress { get; set; }
        public string Description { get; set; }
    }
}
