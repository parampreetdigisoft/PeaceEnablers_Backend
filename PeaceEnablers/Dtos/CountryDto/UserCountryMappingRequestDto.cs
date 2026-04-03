namespace PeaceEnablers.Dtos.CountryDto
{
    public class UserCountryMappingRequestDto
    {
        public int UserId { get; set; }
        public int CountryId { get; set; }
        public int AssignedByUserId { get; set; }
    }
    public class UserCountryUnMappingRequestDto
    {
        public int UserId { get; set; }
        public int AssignedByUserId { get; set; }
    }
}
