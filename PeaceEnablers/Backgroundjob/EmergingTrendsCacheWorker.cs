using Microsoft.Extensions.Configuration;
using PeaceEnablers.IServices;

namespace PeaceEnablers.Backgroundjob
{
    /// <summary>
    /// Refreshes emerging trends in memory on a schedule. Retries every 10s until success (no cache on failure).
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

            _logger.LogInformation(
                "Emerging trends cache worker started (countryCount={CountryCount}, refresh={RefreshMinutes}m, retry={RetrySeconds}s)",
                countryCount,
                refreshInterval.TotalMinutes,
                retryDelay.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                await RefreshUntilSuccessAsync(countryCount, retryDelay, stoppingToken);

                try
                {
                    await Task.Delay(refreshInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task RefreshUntilSuccessAsync(
            int countryCount,
            TimeSpan retryDelay,
            CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var publicService = scope.ServiceProvider.GetRequiredService<IPublicService>();

                    var cached = await publicService.RefreshEmergingTrendsCacheAsync(
                        countryCount,
                        stoppingToken);

                    if (cached)
                    {
                        _logger.LogInformation(
                            "Emerging trends cache refreshed successfully (countryCount={CountryCount})",
                            countryCount);
                        return;
                    }

                    _logger.LogWarning(
                        "Emerging trends refresh returned no data (countryCount={CountryCount}); retry in {RetrySeconds}s",
                        countryCount,
                        retryDelay.TotalSeconds);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(
                        ex,
                        "Emerging trends cache refresh failed (countryCount={CountryCount}); retry in {RetrySeconds}s",
                        countryCount,
                        retryDelay.TotalSeconds);
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
    }
}
