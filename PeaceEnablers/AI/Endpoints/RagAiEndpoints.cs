namespace PeaceEnablers.AI.Endpoints
{
    public static class RagAiEndpoints
    {
        #region ProcessDocument
        public static string ProcessDocument(int documentId) =>
            $"{AiEndpointRoots.Rag}/process-document/{documentId}";
        #endregion

        #region DeleteDocument
        public static string DeleteDocument(int documentId) =>
            $"{AiEndpointRoots.Rag}/delete-document/{documentId}";
        #endregion
    }
}
