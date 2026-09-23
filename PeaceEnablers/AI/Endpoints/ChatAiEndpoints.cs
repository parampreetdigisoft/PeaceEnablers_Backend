namespace PeaceEnablers.AI.Endpoints
{
    public static class ChatAiEndpoints
    {
        #region CountryAsk
        public static string CountryAsk => $"{AiEndpointRoots.Chat}/country";
        #endregion

        #region GlobalAsk
        public static string GlobalAsk => $"{AiEndpointRoots.Chat}/global";
        #endregion

        #region CrossComparision
        public static string CrossComparision => $"{AiEndpointRoots.Chat}/cross-comparision";
        #endregion

        #region CountrySlides
        public static string CountrySlides => $"{AiEndpointRoots.Chat}/executive-slides";
        #endregion

        #region EmergingTrendsAndIssues
        public static string EmergingTrendsAndIssues(int countryCount) =>
            $"{AiEndpointRoots.Chat}/emerging-trends-and-issues?countryCount={countryCount}";
        #endregion

        #region PillarLiveSignals
        public static string PillarLiveSignals => $"{AiEndpointRoots.Chat}/pillar-live-signals";
        #endregion

        #region KpiSummary
        public static string KpiSummary => $"{AiEndpointRoots.Chat}/kpi-summary";
        #endregion
    }
}
