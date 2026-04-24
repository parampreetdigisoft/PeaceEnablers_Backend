namespace PeaceEnablers.Dtos.AiDto
{
    public class AiCountrySummeryDto
    {       
        public int CountryID { get; set; }
        public string? Continent { get; set; }
        public string? CountryName { get; set; }        
        public string? Image { get; set; }
        public int Year { get; set; }
        public decimal? AIScore { get; set; }
        public decimal? AIProgress { get; set; }
        public decimal? EvaluatorScore { get; set; }
        public decimal? Discrepancy { get; set; }

        public string ConfidenceLevel { get; set; }
        public string EvidenceSummary { get; set; }

        public string StructuralEvidence { get; set; }
        public string OperationalEvidence { get; set; }
        public string OutcomeEvidence { get; set; }
        public string PerceptionEvidence { get; set; }
        public string TemporalScope { get; set; }
        public string DistortionScreening { get; set; }
        public string PoliticalShock { get; set; }
        public string EconomicShock { get; set; }
        public string NarrativeShock { get; set; }
        public string OverallStressResilience { get; set; }
        public string StressScoreAdjustment { get; set; }
        public string InequalityAdjustment { get; set; }
        public string OpacityRisk { get; set; }
        public string NonCompensationNote { get; set; }
        public string CrossPillarPatterns { get; set; }
        public string RelationalIntegrity { get; set; }
        public string InstitutionalCapacity { get; set; }
        public string EquityAssessment { get; set; }
        public string ConflictRiskOutlook { get; set; }
        public string StrategicRecommendation { get; set; }
        public string DataTransparencyNote { get; set; }
        public string PrimarySource { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsVerified { get; set; }
        public decimal? AICompletionRate { get; set; }
        public string? ImmediateSituationSummary { get; set; } 
        public string? KeyDevelopments { get; set; }
        public string? CriticalRisks { get; set; }
        public string? Gaps { get; set; }
    }
}
