using PeaceEnablers.Common.Models;

namespace PeaceEnablers.AI
{
    public class AiCallResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public bool IsServiceInactive { get; init; }

        #region Ok

        public static AiCallResult Ok(string? message = null)
        {
            return new AiCallResult
            {
                Success = true,
                Message = message ?? AiMessages.Success.RequestCompleted
            };
        }

        #endregion

        #region Fail

        public static AiCallResult Fail(string? message = null)
        {
            return new AiCallResult
            {
                Success = false,
                Message = message ?? AiMessages.Error.RequestFailed
            };
        }

        #endregion

        #region Inactive

        public static AiCallResult Inactive()
        {
            return new AiCallResult
            {
                Success = false,
                IsServiceInactive = true,
                Message = AiMessages.Error.ServiceNotActive
            };
        }

        #endregion
    }

    public class AiCallResult<T> : AiCallResult where T : class
    {
        public T? Data { get; init; }

        #region Ok

        public static AiCallResult<T> Ok(T data, string? message = null)
        {
            return new AiCallResult<T>
            {
                Success = true,
                Data = data,
                Message = message ?? AiMessages.Success.RequestCompleted
            };
        }

        #endregion

        #region Fail

        public static new AiCallResult<T> Fail(string? message = null)
        {
            return new AiCallResult<T>
            {
                Success = false,
                Message = message ?? AiMessages.Error.RequestFailed
            };
        }

        #endregion

        #region Inactive

        public static new AiCallResult<T> Inactive()
        {
            return new AiCallResult<T>
            {
                Success = false,
                IsServiceInactive = true,
                Message = AiMessages.Error.ServiceNotActive
            };
        }

        #endregion

        #region ToEnvelope

        public T ToEnvelope(Func<T> factory)
        {
            if (Success && Data != null)
            {
                return Data;
            }

            var response = factory();
            if (response is IAiServiceResponse envelope)
            {
                envelope.Success = false;
                envelope.Message = string.IsNullOrWhiteSpace(Message)
                    ? AiMessages.Error.RequestFailed
                    : Message;
            }

            return response;
        }

        #endregion
    }
}
