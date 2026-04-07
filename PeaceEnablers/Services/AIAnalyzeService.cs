using PeaceEnablers.Common.Implementation;
using PeaceEnablers.Common.Models.settings;
using PeaceEnablers.Data;
using PeaceEnablers.IServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace PeaceEnablers.Services
{
    public class AIAnalyzeService : IAIAnalyzeService
    {
        private readonly HttpService _httpService;
        private readonly  string aiUrl = "http://127.0.0.1:8000";
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private Dictionary<string, string> headers;
        public AIAnalyzeService(HttpService httpService, IOptions<AppSettings> appSettings, ApplicationDbContext context, IAppLogger appLogger)
        {
            _httpService = httpService;
            aiUrl = appSettings?.Value?.AiUrl ?? aiUrl;
            _context = context;
            _appLogger = appLogger;
            headers = new Dictionary<string, string> { { "X-API-Key", appSettings?.Value?.AiToken ?? "" } };
        }
        public async Task RunMonthlyJob()
        {
            var newCountriesIds = _context.Countries.Where(x => x.IsActive && !x.IsDeleted ).Select(x => x.CountryID).ToList();
            foreach (var id in newCountriesIds)
            {
                await AnalyzeSingleCountryFull(id);
            }
        }

        public async Task RunEvery2HoursJob()
        {
            try
            {
                await ImportAiScore();
            }
            catch (Exception ex)
            {
               await _appLogger.LogAsync("Error in Running job in Every 2-hour AI ", ex);
            }

        }

        public async Task ImportAiScore()
        {
            // if new city added
            var totalPillar = await _context.Pillars.CountAsync();
            var allCountriesIds = _context.Countries.Where(x=>x.IsActive && !x.IsDeleted).Select(x=>x.CountryID).ToList();
            var importedCountriesIds = _context.AICountryScores.Select(x => x.CountryID);

            var newCountriesIds = allCountriesIds.Where(x=> !importedCountriesIds.Contains(x)).ToList();
            foreach (var id in newCountriesIds)
            {
                await AnalyzeSingleCountryFull(id);
            }

            var now = DateTime.UtcNow;

            // Run at 1st day of every month at 01:00 AM UTC
            var date = new DateTime(now.Year, now.Month, 1, 1, 0, 0, DateTimeKind.Utc)
                            .AddMonths(-1);

            var importPillarscountryIds = _context.AIPillarScores
                .GroupBy(x => x.CountryID)
                .Where(g => g.Max(x => x.UpdatedAt) < date || g.Count() < totalPillar)
                .Select(g => g.Key)
                .ToList();


            foreach (var id in importPillarscountryIds)
            {
                await AnalyzeCountryPillars(id);
            }


            var needtoImportcountryIds = _context.AICountryScores.Where(x => x.UpdatedAt < date).Select(x=>x.CountryID);
            foreach (var id in needtoImportcountryIds)
            {
                await AnalyzeSingleCountry(id);
            }
        }

        public async Task AnalyzeAllCountriesFull()
        {
            var url = aiUrl + AiEndpoints.AnalyzeAllCountriesFull;
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }

        public async Task AnalyzeSingleCountryFull(int countryId)
        {
            var url = aiUrl + AiEndpoints.AnalyzeSingleCountryFull(countryId);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }

        public async Task AnalyzeSingleCountry(int countryId)
        {
            var url = aiUrl + AiEndpoints.AnalyzeSingleCountry(countryId);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }

        public async Task AnalyzeCountryPillars(int countryId)
        {
            var url = aiUrl + AiEndpoints.AnalyzeCountryPillars(countryId);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }
        public async Task AnalyzeSinglePillar(int countryId, int pillarId)
        {
            var url = aiUrl + AiEndpoints.AnalyzeSinglePillar(countryId, pillarId);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }

        public async Task AnalyzeQuestionsOfCountry(int countryId)
        {
            var url = aiUrl + AiEndpoints.AnalyzeCountryQuestions(countryId);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }

        public async Task AnalyzeQuestionsOfCountryPillar(int countryId, int pillarId)
        {
            var url = aiUrl + AiEndpoints.AnalyzeCountryPillarQuestions(countryId, pillarId);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }


    }

    #region AiEndpoints

    public static class AiEndpoints
    {
        private const string BasePath = "/api/countries-score-analysis";

        public static string AnalyzeAllCountriesFull =>
            $"{BasePath}/analyze/full";

        public static string AnalyzeSingleCountryFull(int countryId) =>
            $"{BasePath}/analyze/{countryId}/full";

        public static string AnalyzeSingleCountry(int countryId) =>
            $"{BasePath}/analyze/{countryId}";

        public static string AnalyzeCountryPillars(int countryId) =>
            $"{BasePath}/analyze/{countryId}/pillars";
        public static string AnalyzeSinglePillar(int countryId, int pillarId) =>
            $"{BasePath}/analyze/{countryId}/single-pillar/{pillarId}";

        public static string AnalyzeCountryQuestions(int countryId) =>
            $"{BasePath}/analyze/{countryId}/questions";

        public static string AnalyzeCountryPillarQuestions(int countryId, int pillarId) =>
            $"{BasePath}/analyze/{countryId}/pillars/{pillarId}/questions";
    }

    #endregion
}
