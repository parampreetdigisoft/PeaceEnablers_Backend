using PeaceEnablers.Enums;

namespace PeaceEnablers.Dtos.CountryUserDto
{
    public class UserCountryGetPillarInfoRequestDto
    {
        public int UserID { get; set; } = 0;
        public int CountryID { get; set; }
        public int PillarID { get; set; }
        public DateTime UpdatedAt { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);
        public TieredAccessPlan Tiered { get; set; } = TieredAccessPlan.Pending;
    }
}
