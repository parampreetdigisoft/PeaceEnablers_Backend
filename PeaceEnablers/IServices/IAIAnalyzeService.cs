using PeaceEnablers.Dtos.chatDto;
using PeaceEnablers.Services;

namespace PeaceEnablers.IServices
{
    public interface IAIAnalyzeService
    {
        Task AnalyzeAllCountriesFull();
        Task AnalyzeSingleCountryFull(int countryId);
        Task AnalyzeSingleCountry(int countryId);
        Task AnalyzeCountryPillars(int countryId);
        Task AnalyzeSinglePillar(int countryId, int pillarId);
        Task AnalyzeQuestionsOfCountry(int countryId);
        Task AnalyzeQuestionsOfCountryPillar(int countryId, int pillarId);
        Task AnalyzeCountryMissingQuestions(MissingCountryQuestionRequest r);
        Task ProcessDocument(int documentID);
        Task DeleteDocument(int documentID);
        Task AnalyzeCountryImmediateSituation(int countryId);
        Task<ChatCountryAskQuestionResponse> ChatCountryAsk(ChatCountryAskQuestionRequest request);
        Task<ChatCountryAskQuestionResponse> ChatGlobalAsk(ChatGlobalAskQuestionRequest request);
        Task<ChatCountryAskQuestionResponse> CrossComparision(CrossComparisionRequest request);
        Task<ChatCountryExecutiveSlidesResponse?> GetCountrySlides(int countryId);

        Task RunEvery2HoursJob();
        Task RunDailyJob();
        Task RunMonthlyJob();
    }
}
