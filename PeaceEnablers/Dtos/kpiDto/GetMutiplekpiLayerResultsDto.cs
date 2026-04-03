using PeaceEnablers.Models;

namespace PeaceEnablers.Dtos.kpiDto
{
    public class GetMutiplekpiLayerResultsDto
    {
        public int LayerID { get; set; }
        public string LayerCode { get; set; } = string.Empty;
        public string LayerName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string? CalText1 { get; set; }
        public string? CalText2 { get; set; }
        public string? CalText3 { get; set; }
        public string? CalText4 { get; set; }
        public string? CalText5 { get; set; }
        public List<MutipleCountrieskpiLayerResults> Countries { get; set; } = new();
        public ICollection<FiveLevelInterpretation> FiveLevelInterpretations { get; set; } = new List<FiveLevelInterpretation>();
    }
    public class MutipleCountrieskpiLayerResults
    {
        public int LayerResultID { get; set; }
        public int CountryID { get; set; }
        public int? InterpretationID { get; set; }
        public decimal? NormalizeValue { get; set; }       
        public decimal? CalValue5 { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public int? AiInterpretationID { get; set; }
        public decimal? AiNormalizeValue { get; set; }       
        public decimal? AiCalValue5 { get; set; }
        public DateTime? AiLastUpdated { get; set; }
        public Country? Country { get; set; }
    }
}
