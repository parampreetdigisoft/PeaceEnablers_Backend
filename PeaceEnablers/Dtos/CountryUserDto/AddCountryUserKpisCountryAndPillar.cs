namespace PeaceEnablers.Dtos.CountryUserDto
{
    public class AddCountryUserKpisCountryAndPillar
    {
        public List<int> Countries { get; set; } = new();
        public List<int> Pillars { get; set; } = new();
        public bool IsAllCountries { get; set; }
        //public List<int> Kpis { get; set; }
    }
}
