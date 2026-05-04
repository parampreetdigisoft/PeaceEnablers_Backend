namespace PeaceEnablers.Dtos.chatDto
{
    public class CountryChatRequestDto
    {
        public int CountryID { get; set; }
        public int? PillarID { get; set; }
        public string QuestionText { get; set; }
        public string? HistoryText { get; set; }
        public int? FAQID { get; set; }
    }
}
