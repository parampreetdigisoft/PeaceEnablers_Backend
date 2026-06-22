using Microsoft.Extensions.Configuration;
using PeaceEnablers.IServices;

namespace PeaceEnablers.Backgroundjob
{
    /// <summary>
    /// Refreshes emerging trends in memory on a schedule. Failed refreshes keep serving the last good snapshot.
    /// </summary>
    public class EmergingTrendsCacheWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmergingTrendsCacheWorker> _logger;

        public EmergingTrendsCacheWorker(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<EmergingTrendsCacheWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var countryCount = _configuration.GetValue("EmergingTrendsCache:CountryCount", 8);
            var refreshInterval = TimeSpan.FromMinutes(
                _configuration.GetValue("EmergingTrendsCache:RefreshIntervalMinutes", 10));
            var retryDelay = TimeSpan.FromSeconds(
                _configuration.GetValue("EmergingTrendsCache:RetryDelaySeconds", 10));

            await RefreshUntilCachedAsync(countryCount, retryDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(refreshInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                await TryRefreshAsync(countryCount, stoppingToken);
            }
        }

        private async Task RefreshUntilCachedAsync(
            int countryCount,
            TimeSpan retryDelay,
            CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (await TryRefreshAsync(countryCount, stoppingToken))
                {
                    return;
                }

                try
                {
                    await Task.Delay(retryDelay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task<bool> TryRefreshAsync(int countryCount, CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var publicService = scope.ServiceProvider.GetRequiredService<IPublicService>();

                return await publicService.RefreshEmergingTrendsCacheAsync(
                    countryCount,
                    stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Emerging trends cache refresh failed (countryCount={CountryCount})",
                    countryCount);
                return false;
            }
        }
    }
}
