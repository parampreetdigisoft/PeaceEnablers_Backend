namespace PeaceEnablers.Dtos.CountryDto
{
    public class ExportCountryWithOptionDto
    {
        public bool? IsRanking { get; set; }
        public bool? IsAllCountry { get; set; }
        public bool? IsPillarLevel { get; set; }
        public List<int>? CountryIDs { get; set; }
    }
}
