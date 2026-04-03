namespace PeaceEnablers.Dtos.CountryDto
{
    public class AddUpdateCountryDto
    {
        public int CountryID { get; set; }
        public string Continent { get; set; }       
        public string CountryName { get; set; }
        public string CountryCode { get; set; }
        public string? Region { get; set; }
        public IFormFile? ImageFile { get; set; }
        public string? ImageUrl { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Population { get; set; }
        public decimal Income { get; set; }        
        public string? CountryAliasName { get; set; }
        public string? DevelopmentCategory { get; set; }
        public List<int>? PeerCountries { get; set; }

    }
    public class BulkAddCountryDto
    {
        public List<AddUpdateCountryDto> Countries { get; set; }
    }
}
