namespace PeaceEnablers.Dtos.AiDto
{
    public class ChangedAiCountryEvaluationStatusDto
    {
        public int CountryID { get; set; }
        public bool IsVerified { get; set; }
    }

    public class RegenerateAiSearchDto
    {
        public int CountryID { get; set; }
        public bool CountryEnable { get; set; }
        public bool PillarEnable { get; set; }
        public bool QuestionEnable { get; set; }
        public List<int> ViewerUserIDs { get; set; } = new();
    }
    public class RegeneratePillarAiSearchDto : RegenerateAiSearchDto
    {
        public int PillarID { get; set; }
    }
    public class AddCommentDto
    {
        public int CountryID { get; set; }
        public string Comment { get; set; }

    }
}
