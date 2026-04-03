namespace PeaceEnablers.Models
{
    public class AIEstimatedQuestionScore
    {
        public int QuestionScoreID { get; set; }
        public int CountryID { get; set; }
        public int PillarID { get; set; }
        public int QuestionID { get; set; }
        public int Year { get; set; }

        public decimal? AIScore { get; set; }
        public decimal? AIProgress { get; set; }
        public decimal? EvaluatorScore { get; set; }   // ✅ renamed
        public decimal? Discrepancy { get; set; }

        public string? ConfidenceLevel { get; set; }
        public string? EvidenceSummary { get; set; }

        // New Evidence Dimensions
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

        public string? InequalityAdjustment { get; set; }
        public string? OpacityRisk { get; set; }

        public string? RedFlag { get; set; }   // ✅ renamed

        // Source Metadata
        public string? SourceName { get; set; }
        public string? SourceType { get; set; }
        public string? SourceURL { get; set; }
        public int? SourceDataYear { get; set; }
        public int? SourceHierarchyLevel { get; set; }   // ✅ renamed
        public string? SourceDataExtract { get; set; }
        public int? SourcesConsulted { get; set; }       // ✅ renamed

        public DateTime UpdatedAt { get; set; }

        // Navigation Properties
        public Country? Country { get; set; }
        public Pillar? Pillar { get; set; }
        public Question? Question { get; set; }
    }

}
