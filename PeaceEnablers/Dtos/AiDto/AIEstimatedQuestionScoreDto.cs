using PeaceEnablers.Models;

namespace PeaceEnablers.Dtos.AiDto
{
    public class AIEstimatedQuestionScoreDto
    {
        public int CityID { get; set; }
        public int PillarID { get; set; }
        public int QuestionID { get; set; }
        public string? QuestionText { get; set; }

        public int Year { get; set; }   // ✅ renamed from DataYear

        public decimal? AIScore { get; set; }
        public decimal? AIProgress { get; set; }
        public decimal? EvaluatorScore { get; set; }   // ✅ renamed
        public decimal? Discrepancy { get; set; }

        public string? ConfidenceLevel { get; set; }

        public int? SourcesConsulted { get; set; }   // ✅ renamed

        public string? EvidenceSummary { get; set; }

        // Evidence Dimensions
        public string? StructuralEvidence { get; set; }
        public string? OperationalEvidence { get; set; }
        public string? OutcomeEvidence { get; set; }
        public string? PerceptionEvidence { get; set; }

        public string? TemporalScope { get; set; }
        public string? DistortionScreening { get; set; }
        public string? RelationalDependencies { get; set; }

        // Stress Tests
        public string? StressPoliticalShock { get; set; }
        public string? StressEconomicShock { get; set; }
        public string? StressNarrativeShock { get; set; }
        public string? StressOverallResilienceShock { get; set; }

        public string? InequalityAdjustment { get; set; }   // ✅ renamed
        public string? OpacityRisk { get; set; }

        public string? RedFlag { get; set; }   // ✅ renamed

        // Source Metadata
        public string? SourceType { get; set; }
        public string? SourceName { get; set; }
        public string? SourceURL { get; set; }
        public int? SourceDataYear { get; set; }
        public int? SourceHierarchyLevel { get; set; }   // ✅ renamed
        public string? SourceDataExtract { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
