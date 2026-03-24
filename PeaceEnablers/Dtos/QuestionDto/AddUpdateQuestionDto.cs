using PeaceEnablers.Models;
using System.Text.Json.Serialization;

namespace PeaceEnablers.Dtos.QuestionDto
{
    public class AddUpdateQuestionDto
    {
        public int QuestionID { get; set; } = 0;
        public int PillarID { get; set; }
        public string QuestionText { get; set; }
        public bool IsSelected { get; set; } = false;
        public List<QuestionOption> QuestionOptions { get; set; }
    }
    public class AddBulkQuestionsDto
    {
        public List<AddUpdateQuestionDto> Questions { get; set; }
    }

    public class AssessmentQuestionResponseDto
    {
        public int QuestionID { get; set; } = 0;
        public int ResponseID { get; set; } = 0;
        public int PillarID { get; set; }
        public string QuestionText { get; set; }
        public bool IsSelected { get; set; } = false;
        public List<QuestionOptionDto> QuestionOptions { get; set; }
        public List<HistoryQuestionAnswerRawDto> History { get; set; } = new();
    }
    public class QuestionOptionDto
    {
        public int OptionID { get; set; }
        public int QuestionID { get; set; }
        public string OptionText { get; set; }
        public int? ScoreValue { get; set; }
        public int? DisplayOrder { get; set; }
        public bool IsSelected { get; set; } = false;
        public string Justification { get; set; } 
        public string? Source { get; set; } 
    }
    public class HistoryQuestionAnswerRawDto
    {
        public int UserID { get; set; }
        public int QuestionID { get; set; }
        public int? OptionID { get; set; }
        public string OptionText { get; set; }
        public int? ScoreValue { get; set; }
        public decimal? Progress { get; set; }
        public string Justification { get; set; } = "";
        public string Source { get; set; } = "";
        public string FullName { get; set; } = "";
    }
}
