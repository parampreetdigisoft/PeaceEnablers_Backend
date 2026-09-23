namespace PeaceEnablers.AI.Endpoints
{
    public static class EvaluationAiEndpoints
    {
        #region AnalyzeAllCountriesFull
        public static string AnalyzeAllCountriesFull =>
            $"{AiEndpointRoots.Evaluation}/analyze/full";
        #endregion

        #region AnalyzeSingleCountryFull
        public static string AnalyzeSingleCountryFull(int countryId) =>
            $"{AiEndpointRoots.Evaluation}/analyze/{countryId}/full";
        #endregion

        #region AnalyzeSingleCountry
        public static string AnalyzeSingleCountry(int countryId) =>
            $"{AiEndpointRoots.Evaluation}/analyze/{countryId}";
        #endregion

        #region AnalyzeCountryPillars
        public static string AnalyzeCountryPillars(int countryId) =>
            $"{AiEndpointRoots.Evaluation}/analyze/{countryId}/pillars";
        #endregion

        #region AnalyzeSinglePillar
        public static string AnalyzeSinglePillar(int countryId, int pillarId) =>
            $"{AiEndpointRoots.Evaluation}/analyze/{countryId}/single-pillar/{pillarId}";
        #endregion

        #region AnalyzeCountryQuestions
        public static string AnalyzeCountryQuestions(int countryId) =>
            $"{AiEndpointRoots.Evaluation}/analyze/{countryId}/questions";
        #endregion

        #region AnalyzeCountryPillarQuestions
        public static string AnalyzeCountryPillarQuestions(int countryId, int pillarId) =>
            $"{AiEndpointRoots.Evaluation}/analyze/{countryId}/pillars/{pillarId}/questions";
        #endregion

        #region AnalyzeCountryImmediateSituation
        public static string AnalyzeCountryImmediateSituation(int countryId) =>
            $"{AiEndpointRoots.Evaluation}/analyze/{countryId}/immediateSituation";
        #endregion

        #region AnalyzeCityMissingQuestions
        public static string AnalyzeCityMissingQuestions =>
            $"{AiEndpointRoots.Evaluation}/analyze/missing-pillar-questions";
        #endregion
    }
}
