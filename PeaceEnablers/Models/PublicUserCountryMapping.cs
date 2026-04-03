namespace PeaceEnablers.Models
{
    public class PublicUserCountryMapping
    {
        public int PublicUserCountryMappingID { get; set; }
        public int UserID { get; set; }
        public int CountryID { get; set; }
        public Country? Country { get; set; }
        public User? User { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
    }
    public class CountryUserPillarMapping
    {
        public int CountryUserPillarMappingID { get; set; }
        public int PillarID { get; set; }
        public int UserID { get; set; }
        public Pillar? Country { get; set; }
        public User? User { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
    }
    public class CountryUserKpiMapping
    {
        public int CountryUserKpiMappingID { get; set; }
        public int LayerID { get; set; }
        public int UserID { get; set; }
        public AnalyticalLayer? Layer { get; set; }
        public User? User { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
    }
}
