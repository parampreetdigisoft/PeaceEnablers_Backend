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

    public class CountryRankingResultDto
    {
        public int CountryID { get; set; }
        public string CountryName { get; set; }
        public string Region { get; set; }
        public string Continent { get; set; }
        public int TotalCountry { get; set; }
        public int CountryRank { get; set; }
        public int TotalPillarInRegion { get; set; }
        public int RegionRank { get; set; }
        public decimal? CountryAIScore { get; set; }
        public int? DataYear { get; set; }
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
    public class CountryPillarRankingResultDto
    {
        public int CountryID { get; set; }
        public string CountryName { get; set; }
        public string Region { get; set; }
        public string Continent { get; set; }
        public int PillarID { get; set; }
        public int TotalPillars { get; set; }
        public int TotalPillarsInAllCountries { get; set; }
        public int GlobalPillarRank { get; set; }
        public int CountryPillarRank { get; set; }
        public int TotalPillarInRegion { get; set; }
        public int RegionPillarRank { get; set; }
        public decimal? CountryPillarScore { get; set; }
        public int? Year { get; set; }
    }
}
