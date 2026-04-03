namespace AssessmentPlatform.Models
{
    public class CountryPeer
    {
        public int CountryPeerID { get; set; }
        public int CountryID { get; set; }
        public int PeerCountryID { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? UpdatedDate { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
