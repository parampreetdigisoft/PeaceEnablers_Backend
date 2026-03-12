using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PeaceEnablers.Models
{
    public class AIPillarScore
    {
        public int PillarScoreID { get; set; }
        public int CityID { get; set; }
        public int PillarID { get; set; }
        public int Year { get; set; }
        public decimal? AIScore { get; set; }
        public decimal? AIProgress { get; set; }
        public decimal? EvaluatorScore { get; set; }
        public decimal? Discrepancy { get; set; }
        public string? ConfidenceLevel { get; set; }
        public string? EvidenceSummary { get; set; }
        public string? StructuralEvidence { get; set; }
        public string? OperationalEvidence { get; set; }
        public string? OutcomeEvidence { get; set; }
        public string? PerceptionEvidence { get; set; }
        public string? TemporalScope { get; set; }
        public string? DistortionScreening { get; set; }
        public string? RelationalIntegrity { get; set; }
        public string? StressPoliticalShock { get; set; }
        public string? StressEconomicShock { get; set; }
        public string? StressNarrativeShock { get; set; }      
        public string? StressOverallResilience { get; set; }
        public string? StressScoreAdjustment { get; set; }
        public string? InequalityAdjustment { get; set; }
        public string? OpacityRisk { get; set; }
        public string? NonCompensationNote { get; set; }
        public string? GeographicEquityNote { get; set; }
        public string? InstitutionalAssessment { get; set; }
        public string? DataGapAnalysis { get; set; }
        public string? RedFlag { get; set; }

        [Required]
        public DateTime UpdatedAt { get; set; }
        public City? City { get; set; }
        public Pillar? Pillar { get; set; }
        public ICollection<AIDataSourceCitation>? DataSourceCitations { get; set; }
    }

}
