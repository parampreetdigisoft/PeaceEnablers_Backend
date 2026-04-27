namespace PeaceEnablers.Common.Models
{
    public class AiPillarStatsLast4MonthsView
    {
        public int PillarID { get; set; }
        public int CountryID { get; set; }
        public int MonthNo { get; set; }
        public decimal TotalScore { get; set; }
        public int TotalAns { get; set; }
        public int TotalNA { get; set; }
        public int TotalUnknown { get; set; }
        public decimal ScoreProgress { get; set; }
        public decimal NormalizedValue { get; set; }
    }
}
