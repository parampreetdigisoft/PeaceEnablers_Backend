namespace PeaceEnablers.Dtos.CountryDto
{
    public class GetNearestCountryRequestDto
    {
        public int UserID { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
