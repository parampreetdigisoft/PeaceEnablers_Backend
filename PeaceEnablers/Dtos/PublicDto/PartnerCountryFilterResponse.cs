namespace PeaceEnablers.Dtos.PublicDto
{
    public class PartnerCountryFilterResponse
    {
        public List<string> Countries { get; set; }
        public List<string> Regions { get; set; }
        public List<PartnerCountryDto> Cities { get; set; }
    }

    public class PartnerCountryDto
    {
        public int CountryID { get; set; }
        public string CountryName { get; set; }
    }
}
