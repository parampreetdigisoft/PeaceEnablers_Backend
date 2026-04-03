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

        Task RunEvery2HoursJob();
        Task RunMonthlyJob();
    }
}
