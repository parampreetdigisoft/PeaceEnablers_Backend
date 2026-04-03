namespace PeaceEnablers.Dtos.CountryDto
{
    public class EvaluationCountryProgressResultDto
    {
        public int PillarID { get; set; }
        public double Weight { get; set; }
        public bool Reliability { get; set; }
        public int CountryID { get; set; }
        public int TotalScore { get; set; }
        public int TotalAns { get; set; }
        public decimal ScoreProgress { get; set; }
        public decimal AIProgress { get; set; }
        public decimal NormalizedValue { get; set; }
        public int TotalAssessments { get; set; }
        public int UserID { get; set; }
    }
    public class EvaluationCountryProgressHistoryResultDto
    {
        public int PillarID { get; set; }
        public double Weight { get; set; }
        public bool Reliability { get; set; }
        public int CountryID { get; set; }
        public int TotalScore { get; set; }
        public int TotalAns { get; set; }
        public decimal ScoreProgress { get; set; }
        public int Year { get; set; }
        public decimal NormalizedValue { get; set; }
        public int TotalAssessments { get; set; }
        public int UserID { get; set; }
    }
}
