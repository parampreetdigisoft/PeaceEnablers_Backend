namespace PeaceEnablers.Dtos.AiDto
{
    public class UpdateAICountryScoreDto
    {
        public int CountryID { get; set; }
        public int Year { get; set; }
        public decimal? AIProgress { get; set; }
        public decimal? EvaluatorScore { get; set; }
        public string? ConfidenceLevel { get; set; }
        public string? ImmediateSituationSummary { get; set; }
        public string? EvidenceSummary { get; set; }
        public string? KeyDevelopments { get; set; }
        public string? CriticalRisks { get; set; }
        public string? Gaps { get; set; }
        public string? StructuralEvidence { get; set; }
        public string? OperationalEvidence { get; set; }
        public string? OutcomeEvidence { get; set; }
        public string? PerceptionEvidence { get; set; }
        public string? TemporalScope { get; set; }
        public string? DistortionScreening { get; set; }
        public string? PoliticalShock { get; set; }
        public string? EconomicShock { get; set; }
        public string? NarrativeShock { get; set; }
        public string? StressScoreAdjustment { get; set; }
        public string? InequalityAdjustment { get; set; }
        public string? OpacityRisk { get; set; }
        public string? NonCompensationNote { get; set; }
        public string? RelationalIntegrity { get; set; }
        public string? InstitutionalCapacity { get; set; }
        public string? PrimarySource { get; set; }
        public string? CrossPillarPatterns { get; set; }
        public string? EquityAssessment { get; set; }
        public string? ConflictRiskOutlook { get; set; }
        public string? StrategicRecommendation { get; set; }
        public string? DataTransparencyNote { get; set; }
        public string? KeyFindings { get; set; }
        public string? Recommendations { get; set; }
    }

    public class UpdateAIPillarScoreDto
    {
        public int PillarScoreID { get; set; }
        public decimal? AIProgress { get; set; }
        public decimal? EvaluatorScore { get; set; }
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
        public string? StressScoreAdjustment { get; set; }
        public string? InequalityAdjustment { get; set; }
        public string? OpacityRisk { get; set; }
        public string? NonCompensationNote { get; set; }
        public string? GeographicEquityNote { get; set; }
        public string? InstitutionalAssessment { get; set; }
        public string? DataGapAnalysis { get; set; }
        public string? RedFlag { get; set; }
        public List<UpdateAIDataSourceCitationDto>? DataSourceCitations { get; set; }
    }

    public class UpdateAIDataSourceCitationDto
    {
        public int CitationID { get; set; }
        public string? SourceType { get; set; }
        public string? SourceName { get; set; }
        public string? SourceURL { get; set; }
        public int? DataYear { get; set; }
        public string? DataExtract { get; set; }
        public int? TrustLevel { get; set; }
    }

    public class UpdateAIEstimatedQuestionScoreDto
    {
        public int CountryID { get; set; }
        public int PillarID { get; set; }
        public int QuestionID { get; set; }
        public int Year { get; set; }
        public decimal? AIScore { get; set; }
        public decimal? EvaluatorScore { get; set; }
        public string? ConfidenceLevel { get; set; }
        public int? SourcesConsulted { get; set; }
        public string? EvidenceSummary { get; set; }
        public string? StructuralEvidence { get; set; }
        public string? OperationalEvidence { get; set; }
        public string? OutcomeEvidence { get; set; }
        public string? PerceptionEvidence { get; set; }
        public string? TemporalScope { get; set; }
        public string? DistortionScreening { get; set; }
        public string? RelationalDependencies { get; set; }
        public string? StressPoliticalShock { get; set; }
        public string? StressEconomicShock { get; set; }
        public string? StressNarrativeShock { get; set; }
        public string? StressOverallResilienceShock { get; set; }
        public string? InequalityAdjustment { get; set; }
        public string? OpacityRisk { get; set; }
        public string? RedFlag { get; set; }
        public string? SourceType { get; set; }
        public string? SourceName { get; set; }
        public string? SourceURL { get; set; }
        public int? SourceDataYear { get; set; }
        public int? SourceHierarchyLevel { get; set; }
        public string? SourceDataExtract { get; set; }
    }
}
