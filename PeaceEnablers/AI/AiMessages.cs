namespace PeaceEnablers.AI
{
    public static class AiMessages
    {
        public static class Success
        {
            public const string RequestCompleted = "AI request completed successfully.";
            public const string AnalysisStarted = "Analysis request sent successfully.";
            public const string DocumentProcessed = "Document processing request sent successfully.";
            public const string DocumentDeleted = "Document deletion request sent successfully.";
        }

        public static class Error
        {
            public const string ServiceNotActive = "AI service is not active.";
            public const string ServiceUnreachable = "AI service is currently unavailable.";
            public const string RequestFailed = "Failed to complete the AI request.";
            public const string EmptyResponse = "AI service returned an empty response.";
            public const string InvalidConfiguration = "AI service is not configured.";
        }

        public static class Log
        {
            public const string MonthlyJobFailed = "Error in Run Monthly Job";
            public const string TwoHourJobFailed = "Error in Running job in Every 2-hour AI ";
            public const string DailyJobFailed = "Error in Running job in Run daily job ";
            public const string ServiceUnreachable = "AI service is currently unavailable.";
        }
    }
}
