namespace PeaceEnablers.Dtos.chatDto
{
    public class PerformanceSummary
    {
        public string Trend { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;
    }

    public class CombinedRiskItem
    {
        public int Rank { get; set; }

        public string Title { get; set; } = string.Empty;

        public int RiskScore { get; set; }

        public string Severity { get; set; } = string.Empty;

        public string Trend { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Recommendation { get; set; } = string.Empty;
    }

    public class EarlyWarningItem
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Timeframe { get; set; } = string.Empty;

        public string ImpactLevel { get; set; } = string.Empty;
    }

    public class CountryExecutiveSlidesResult
    {
        public int CountryId { get; set; }

        public string CountryName { get; set; } = string.Empty;

        public PerformanceSummary RecentPerformance { get; set; } = new();

        public List<CombinedRiskItem> CombinedRisks { get; set; } = new();

        public List<EarlyWarningItem> EarlyWarnings { get; set; } = new();
    }

    public class ChatCountryExecutiveSlidesResponse
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public CountryExecutiveSlidesResult Result { get; set; } = new();
    }

    public class CountrySlidesRequest
    {
        public int CountryId { get; set; }
    }
}
