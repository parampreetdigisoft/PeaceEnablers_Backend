namespace PeaceEnablers.Dtos.AssessmentDto
{
    public class GetCountryQuestionHistoryResponseDto : GetCountrySubmitionHistoryResponseDto
    {
        public List<CountryPillarQuestionHistoryResponseDto> Pillars { get; set; } = new();
    }
    public class GetCountrySubmitionHistoryResponseDto
    {
        public int CountryID { get; set; }
        public int TotalAssessment { get; set; } = 0;
        public decimal Score { get; set; } = 0;
        public decimal ScoreProgress { get; set; } = 0;
        public decimal AIScore { get; set; } = 0;
        public int TotalPillar { get; set; } = 0;
        public int TotalAnsPillar { get; set; } = 0;
        public int TotalQuestion { get; set; } = 0;
        public int AnsQuestion { get; set; } = 0;
    }
    public class GetCountriesSubmitionHistoryResponseDto : GetCountrySubmitionHistoryResponseDto
    {
        public string CountryName { get; set; }
    }

    public class CountryPillarQuestionHistoryResponseDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public string ImagePath { get; set; }
        public bool IsAccess { get; set; } = false;
        public decimal Score { get; set; }=0;
        public decimal ScoreProgress { get; set; } = 0;
        public int AnsPillar { get; set; } = 0;
        public int TotalQuestion { get; set; } = 0;
        public int AnsQuestion { get; set; } = 0;
        public int DisplayOrder { get; set; } = 0;
    }
    public class GetAssessmentHistoryDto
    {
        public int AssessmentID { get; set; }
        public double Score { get; set; }
        public int TotalAnsPillar { get; set; }
        public int TotalQuestion { get; set; }
        public int TotalAnsQuestion { get; set; }
        public double CurrentProgress { get; set; }
    }

    public class CountryPillarUserHistoryResponseDto : CountryPillarQuestionHistoryResponseDto
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
    }
}
