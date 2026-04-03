using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Enums;
using PeaceEnablers.Models;

namespace PeaceEnablers.Dtos.AssessmentDto
{

    public class GetPillarResponseHistoryRequestNewDto : PaginationRequest
    {
        public int CountryID { get; set; }
        public int? PillarID { get; set; }
        public DateTime UpdatedAt { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);
    }
    public class GetCountryPillarHistoryRequestDto
    {
        public int UserID { get; set; }
        public int CountryID { get; set; }
        public int? PillarID { get; set; }
        public DateTime UpdatedAt { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);

        public ExportType ExportType { get; set; }
    }
    public class UserCountryRequestDto : UserCountryDashBoardRequestDto
    {
        public int UserID { get; set; }
        public TieredAccessPlan Tiered { get; set; } = TieredAccessPlan.Pending;
    }
    public class UserCountryDashBoardRequestDto
    {
        public int CountryID { get; set; }
        public DateTime UpdatedAt { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);
    }

    public class PillarWithQuestionsDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public int DisplayOrder { get; set; }
        public int TotalQuestions { get; set; }
        public List<QuestionWithUserDto> Questions { get; set; } = new();
    }

    public class QuestionWithUserDto
    {
        public int QuestionID { get; set; }
        public string QuestionText { get; set; }
        public int DisplayOrder { get; set; }
        public Dictionary<int, QuestionUserAnswerDto> Users { get; set; } = new();
    }

    public class QuestionUserAnswerDto
    {
        public int UserID { get; set; }
        public int? QuestionID { get; set; }
        public string FullName { get; set; }
        public int? Score { get; set; }
        public string Justification { get; set; }
        public string OptionText { get; set; }
    }
    public class ChangeAssessmentStatusRequestDto
    {
        public int UserID { get; set; }
        public int AssessmentID { get; set; }
        public AssessmentPhase AssessmentPhase { get; set; }
    }
    public class QuestionPdfRowDto
    {
        public string PillarName { get; set; }
        public string QuestionText { get; set; }
        public string UserName { get; set; }
        public int? Score { get; set; }
        public string OptionText { get; set; }
        public string Justification { get; set; }
    }
}
