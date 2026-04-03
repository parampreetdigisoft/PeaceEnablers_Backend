namespace PeaceEnablers.Models
{
    public class UserCountryMapping
    {
        public int UserCountryMappingID { get; set; }
        public int UserID { get; set; }
        public UserRole Role { get; set; }
        public int CountryID { get; set; }
        public int AssignedByUserId { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
