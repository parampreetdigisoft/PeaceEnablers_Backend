using PeaceEnablers.Models;

namespace PeaceEnablers.Dtos.AiDto
{

    public class AiCountryPillarResponseDto
    {
        public List<AiCountryPillarResponse> Pillars { get; set; }
    }
    public class AiCountryPillarResponse
    {
        public int PillarScoreID { get; set; }

        public int CountryID { get; set; }
        public string? Continent { get; set; }
        public string? CountryName { get; set; }        

        public int PillarID { get; set; }
        public string? PillarName { get; set; }
        public int DisplayOrder { get; set; }
        public string? ImagePath { get; set; }

        public bool IsAccess { get; set; } = false;

        public int AIDataYear { get; set; }

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

        public string? RedFlag { get; set; }   // renamed from RedFlags → RedFlag (matches DB)

        public decimal? AICompletionRate { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public ICollection<AIDataSourceCitation>? DataSourceCitations { get; set; }
    }
}
