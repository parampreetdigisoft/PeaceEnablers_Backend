using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Dtos.chatDto;

namespace PeaceEnablers.IServices
{
    public interface IAIAnalyzeService
    {
        #region AnalyzeAllCountriesFull
        Task AnalyzeAllCountriesFull();
        #endregion

        #region AnalyzeSingleCountryFull
        Task AnalyzeSingleCountryFull(int countryId);
        #endregion

        #region AnalyzeSingleCountry
        Task AnalyzeSingleCountry(int countryId);
        #endregion

        #region AnalyzeCountryPillars
        Task AnalyzeCountryPillars(int countryId);
        #endregion

        #region AnalyzeSinglePillar
        Task AnalyzeSinglePillar(int countryId, int pillarId);
        #endregion

        #region AnalyzeQuestionsOfCountry
        Task AnalyzeQuestionsOfCountry(int countryId);
        #endregion

        #region AnalyzeQuestionsOfCountryPillar
        Task AnalyzeQuestionsOfCountryPillar(int countryId, int pillarId);
        #endregion

        #region AnalyzeCountryMissingQuestions
        Task AnalyzeCountryMissingQuestions(MissingCountryQuestionRequest r);
        #endregion

        #region ProcessDocument
        Task ProcessDocument(int documentID);
        #endregion

        #region DeleteDocument
        Task DeleteDocument(int documentID);
        #endregion

        #region AnalyzeCountryImmediateSituation
        Task AnalyzeCountryImmediateSituation(int countryId);
        #endregion

        #region ChatCountryAsk
        Task<ChatCountryAskQuestionResponse> ChatCountryAsk(ChatCountryAskQuestionRequest request);
        #endregion

        #region ChatGlobalAsk
        Task<ChatCountryAskQuestionResponse> ChatGlobalAsk(ChatGlobalAskQuestionRequest request);
        #endregion

        #region CrossComparision
        Task<ChatCountryAskQuestionResponse> CrossComparision(CrossComparisionRequest request);
        #endregion

        #region SummarizeKpiPerformance
        Task<KpiSummaryAiResponse?> SummarizeKpiPerformance(KpiSummaryAiRequest request);
        #endregion

        #region GetCountrySlides
        Task<ChatCountryExecutiveSlidesResponse?> GetCountrySlides(int countryId);
        #endregion

        #region GetEmergingTrendsAndIssues
        Task<ChatEmergingTrendsResponse?> GetEmergingTrendsAndIssues(int countryCount);
        #endregion

        #region GetPillarLiveSignals
        Task<ChatPillarLiveSignalsResponse?> GetPillarLiveSignals();
        #endregion

        #region RunEvery2HoursJob
        Task RunEvery2HoursJob();
        #endregion

        #region RunDailyJob
        Task RunDailyJob();
        #endregion

        #region RunMonthlyJob
        Task RunMonthlyJob();
        #endregion
    }
}
