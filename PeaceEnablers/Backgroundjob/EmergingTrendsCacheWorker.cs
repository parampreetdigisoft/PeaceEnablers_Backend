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

        public EmergingTrendsCacheWorker(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<EmergingTrendsCacheWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var countryCount = _configuration.GetValue("EmergingTrendsCache:CountryCount", 8);
            var refreshInterval = TimeSpan.FromMinutes(
                _configuration.GetValue("EmergingTrendsCache:RefreshIntervalMinutes", 10));
            var retryDelay = TimeSpan.FromSeconds(
                _configuration.GetValue("EmergingTrendsCache:RetryDelaySeconds", 10));

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
                    var _appLogger = scope.ServiceProvider.GetRequiredService<IAppLogger>();

                    var cached = await publicService.RefreshEmergingTrendsCacheAsync(
                        countryCount,
                        stoppingToken);

                    if (cached)
                    {
                        //await _appLogger.LogAsync($"Emerging trends cache refreshed successfully (countryCount={countryCount})");
                        return;
                    }

                    //await _appLogger.LogAsync($"Emerging trends refresh returned no data (countryCount={countryCount}); retry in {retryDelay.TotalSeconds}s");

                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var _appLogger = scope.ServiceProvider.GetRequiredService<IAppLogger>();
                    await _appLogger.LogAsync($"Emerging trends cache refresh failed (countryCount={countryCount}); retry in {retryDelay.TotalSeconds}s");
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
