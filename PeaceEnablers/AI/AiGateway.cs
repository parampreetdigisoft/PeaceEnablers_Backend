using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PeaceEnablers.Common.Implementation;
using PeaceEnablers.Common.Models.settings;
using PeaceEnablers.IServices;

namespace PeaceEnablers.AI
{
    public class AiGateway : IAiGateway
    {
        #region constructor

        private const string UnreachableCacheKey = "ai.gateway.unreachable";
        private const string HeaderApiKey = "X-API-Key";
        private const string HeaderUserId = "X-User-Id";
        private const string HeaderUserRoles = "X-User-Roles";
        private const string HeaderUserEmail = "X-User-Email";
        private const string BackgroundUserId = "system";
        private const string BackgroundUserRole = "Admin";
        private static readonly TimeSpan UnreachableCacheDuration = TimeSpan.FromSeconds(30);

        private readonly HttpService _httpService;
        private readonly IAppLogger _appLogger;
        private readonly IMemoryCache _cache;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly string _aiUrl;
        private readonly bool _isConfiguredActive;
        private readonly string _apiKey;

        public AiGateway(
            HttpService httpService,
            IOptions<AppSettings> appSettings,
            IAppLogger appLogger,
            IMemoryCache cache,
            IHttpContextAccessor httpContextAccessor)
        {
            _httpService = httpService;
            _appLogger = appLogger;
            _cache = cache;
            _httpContextAccessor = httpContextAccessor;

            var settings = appSettings?.Value;
            _aiUrl = string.IsNullOrWhiteSpace(settings?.AiUrl)
                ? "http://127.0.0.1:8000"
                : settings.AiUrl.TrimEnd('/');
            _isConfiguredActive = settings?.IsAiActive != false;
            _apiKey = settings?.AiToken ?? string.Empty;
        }

        #endregion

        #region IsActive

        public bool IsActive
        {
            get
            {
                if (!_isConfiguredActive)
                {
                    return false;
                }

                if (_cache.TryGetValue(UnreachableCacheKey, out bool unreachable) && unreachable)
                {
                    return false;
                }

                return true;
            }
        }

        #endregion

        #region ExecuteAsync

        public async Task<AiCallResult> ExecuteAsync(HttpMethod method, string relativePath, object? body = null)
        {
            if (!IsActive)
            {
                return AiCallResult.Inactive();
            }

            try
            {
                await _httpService.SendAsync<object>(method, BuildUrl(relativePath), body, CreateHeaders());
                return AiCallResult.Ok();
            }
            catch (Exception ex)
            {
                MarkUnreachable();
                await _appLogger.LogAsync(AiMessages.Log.ServiceUnreachable, ex);
                return AiCallResult.Inactive();
            }
        }

        #endregion

        #region SendAsync

        public async Task<AiCallResult<T>> SendAsync<T>(HttpMethod method, string relativePath, object? body = null) where T : class
        {
            if (!IsActive)
            {
                return AiCallResult<T>.Inactive();
            }

            try
            {
                var data = await _httpService.SendAsync<T>(method, BuildUrl(relativePath), body, CreateHeaders());
                if (data == null)
                {
                    return AiCallResult<T>.Fail(AiMessages.Error.EmptyResponse);
                }

                return AiCallResult<T>.Ok(data);
            }
            catch (Exception ex)
            {
                MarkUnreachable();
                await _appLogger.LogAsync(AiMessages.Log.ServiceUnreachable, ex);
                return AiCallResult<T>.Inactive();
            }
        }

        #endregion

        #region BuildUrl

        private string BuildUrl(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return _aiUrl;
            }

            return relativePath.StartsWith("/")
                ? _aiUrl + relativePath
                : _aiUrl + "/" + relativePath;
        }

        #endregion

        #region CreateHeaders

        /// <summary>
        /// Builds AI request headers. Chat/user requests forward JWT UserId + Role;
        /// background jobs (no authenticated HttpContext) send system identity.
        /// </summary>
        private Dictionary<string, string> CreateHeaders()
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { HeaderApiKey, _apiKey }
            };

            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var userId = user.FindFirst("UserId")?.Value
                    ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var roles = user.FindAll(ClaimTypes.Role)
                    .Select(c => c.Value)
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var email = user.FindFirst(ClaimTypes.Email)?.Value
                    ?? user.FindFirst(ClaimTypes.Name)?.Value;

                headers[HeaderUserId] = string.IsNullOrWhiteSpace(userId)
                    ? BackgroundUserId
                    : userId.Trim();
                headers[HeaderUserRoles] = roles.Count > 0 
                    ? string.Join(",", roles)
                    : BackgroundUserRole;

                if (!string.IsNullOrWhiteSpace(email))
                {
                    headers[HeaderUserEmail] = email.Trim();
                }
            }
            else
            {
                // Background workers / public refresh paths — connect with system params
                headers[HeaderUserId] = BackgroundUserId;
                headers[HeaderUserRoles] = BackgroundUserRole;
            }

            return headers;
        }

        #endregion

        #region MarkUnreachable

        private void MarkUnreachable()
        {
            _cache.Set(UnreachableCacheKey, true, UnreachableCacheDuration);
        }

        #endregion
    }
}
