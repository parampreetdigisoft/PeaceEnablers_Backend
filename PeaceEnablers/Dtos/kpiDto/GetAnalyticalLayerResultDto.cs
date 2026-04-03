using PeaceEnablers.Models;

namespace PeaceEnablers.Dtos.kpiDto
{
    public class GetAnalyticalLayerResultDto
    {
        public int LayerResultID { get; set; }
        public int LayerID { get; set; }
        public int CountryID { get; set; }
        public int? InterpretationID { get; set; }
        public decimal? NormalizeValue { get; set; }       
        public decimal? CalValue5 { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public int? AiInterpretationID { get; set; }
        public decimal? AiNormalizeValue { get; set; }       
        public decimal? AiCalValue5 { get; set; }
        public DateTime? AiLastUpdated { get; set; }
        public string LayerCode { get; set; } = string.Empty;
        public string LayerName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;       
        public string? CalText5 { get; set; }
        public ICollection<FiveLevelInterpretation> FiveLevelInterpretations { get; set; } = new List<FiveLevelInterpretation>();
        public Country? Country { get; set; }
    }
}
