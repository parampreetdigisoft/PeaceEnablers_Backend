namespace PeaceEnablers.AI
{
    public interface IAiGateway
    {
        #region IsActive
        bool IsActive { get; }
        #endregion

        #region ExecuteAsync
        Task<AiCallResult> ExecuteAsync(HttpMethod method, string relativePath, object? body = null);
        #endregion

        #region SendAsync
        Task<AiCallResult<T>> SendAsync<T>(HttpMethod method, string relativePath, object? body = null) where T : class;
        #endregion
    }
}
