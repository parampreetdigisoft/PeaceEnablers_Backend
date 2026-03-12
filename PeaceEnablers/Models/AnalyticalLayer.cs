namespace PeaceEnablers.Models
{
    public class AnalyticalLayer
    {
        public int LayerID { get; set; }
        public string LayerCode { get; set; } = string.Empty;
        public string LayerName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;       
        public string? CalText5 { get; set; }
        public bool IsDeleted { get; set; } = false;
        public ICollection<AnalyticalLayerResult> AnalyticalLayerResults { get; set; } = new List<AnalyticalLayerResult>();
        public ICollection<FiveLevelInterpretation> FiveLevelInterpretations { get; set; } = new List<FiveLevelInterpretation>();
    }
}
