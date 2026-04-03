using PeaceEnablers.Models;

namespace PeaceEnablers.Dtos.QuestionDto
{
    public class GetQuestionResponse : AddUpdateQuestionDto
    {
        public int DisplayOrder { get; set; }
        public string PillarName { get; set; }
    }
    public class GetQuestionByCountryResponse : GetQuestionResponse
    {
        public int AssessmentID { get; set; }
        public int PillarDisplayOrder { get; set; }
    }
    public class GetPillarQuestionByCountryResponse 
    {
        public int AssessmentID { get; set; }
        public int UserCountryMappingID { get; set; }
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public int DisplayOrder { get; set; }
        public string Description { get; set; }
        public int SubmittedPillarDisplayOrder { get; set; }
        public List<AssessmentQuestionResponseDto> Questions { get; set; }
    }
}
