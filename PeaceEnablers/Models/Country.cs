using AssessmentPlatform.Models;

namespace PeaceEnablers.Models
{
    public class Country
    {
        public int CountryID { get; set; }
        public string CountryName { get; set; }       
        public string Continent { get; set; }      
        public string? CountryCode { get; set; }      
        public string? Region { get; set; }
        public bool IsActive { get; set; }  = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? UpdatedDate { get; set; }
        public bool IsDeleted { get; set; } = false;        
        public string? Image { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? DevelopmentCategory { get; set; }
        public int? Population { get; set; }
        public decimal? Income { get; set; }       
        public string? CountryAliasName { get; set; }
        public ICollection<CountryPeer>? CountryPeers { get; set; }
    }
}
