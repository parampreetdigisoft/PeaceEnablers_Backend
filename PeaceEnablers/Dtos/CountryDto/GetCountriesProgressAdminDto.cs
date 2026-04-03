namespace PeaceEnablers.Dtos.CountryDto
{
    public class GetCountriesProgressAdminDto
    {
        public int CountryID { get; set; }
        public string CountryName { get; set; }
        public string Continent { get; set; }        
        public int PillarID { get; set; }
        public string PillarName { get; set; } 
        public int DisplayOrder { get; set; }
        public int TotalScore { get; set; }
        public int TotalAns { get; set; }
        public decimal PillarProgress { get; set; }
        public decimal AIPillarProgress { get; set; }
        public decimal AICountryProgress { get; set; }
    }
}
