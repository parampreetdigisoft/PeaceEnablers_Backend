namespace PeaceEnablers.Dtos.chatDto
{
    public class CountryChatRequestDto : ChatGlobalAskQuestionRequestDto
    {
        public int CountryID { get; set; }
        public int? PillarID { get; set; }
    }
    public class ChatGlobalAskQuestionRequestDto
    {
        public string QuestionText { get; set; }
        public string? HistoryText { get; set; }
        public int? FAQID { get; set; }
    }
}
