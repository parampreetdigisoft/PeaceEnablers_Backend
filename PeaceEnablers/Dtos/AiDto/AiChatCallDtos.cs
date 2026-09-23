using PeaceEnablers.Common.Models;

namespace PeaceEnablers.Dtos.AiDto
{
    public class ChatGlobalAskQuestionRequest
    {
        public string QuestionText { get; set; }
        public string? HistoryText { get; set; }
        public int? FAQID { get; set; }
    }

    public class ChatCountryAskQuestionRequest : ChatGlobalAskQuestionRequest
    {
        public int CountryID { get; set; }
        public int? PillarID { get; set; }
    }

    public class CrossComparisionRequest
    {
        public List<int> CountryIDs { get; set; }
        public string QuestionText { get; set; }
        public string? HistoryText { get; set; }
    }

    public class ChatCountryAskQuestionResponse : IAiServiceResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Result { get; set; }
    }
}
