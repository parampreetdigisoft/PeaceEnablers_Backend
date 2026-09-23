namespace PeaceEnablers.AI.Endpoints
{
    public static class AdminAiEndpoints
    {
        #region Health
        public static string Health => $"{AiEndpointRoots.Admin}/health";
        #endregion

        #region Jobs
        public static string Jobs => $"{AiEndpointRoots.Admin}/jobs";
        #endregion

        #region AllJobs
        public static string AllJobs => $"{AiEndpointRoots.Admin}/jobs/all";
        #endregion

        #region CountryStatus
        public static string CountryStatus(int countryId) =>
            $"{AiEndpointRoots.Admin}/countries/{countryId}/status";
        #endregion

        #region CancelJob
        public static string CancelJob(string jobId) =>
            $"{AiEndpointRoots.Admin}/jobs/{jobId}/cancel";
        #endregion

        #region GetJob
        public static string GetJob(string jobId) => $"{AiEndpointRoots.Jobs}/{jobId}";
        #endregion
    }
}
