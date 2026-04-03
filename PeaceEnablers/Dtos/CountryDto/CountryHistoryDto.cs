namespace PeaceEnablers.Dtos.CountryDto
{
    public class CountryHistoryDto
    {
        public int TotalCountry{ get; set; }
        public int TotalAnalyst { get; set; }
        public int TotalEvaluator { get; set; }
        public int ActiveCountry { get; set; }
        public int TotalAccessCountry { get; set; }
        public int CompeleteCountry { get; set; }
        public int InprocessCountry { get; set; }
        public decimal AvgHighScore { get; set; }
        public decimal AvgLowerScore { get; set; }
        public decimal OverallVitalityScore { get; set; }
        public int FinalizeCountry { get; set; }
        public int UnFinalize { get; set; }
    }
}
