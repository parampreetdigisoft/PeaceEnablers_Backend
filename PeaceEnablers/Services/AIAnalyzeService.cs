using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PeaceEnablers.Common.Implementation;
using PeaceEnablers.Common.Models.settings;
using PeaceEnablers.Data;
using PeaceEnablers.Dtos.chatDto;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;

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
            try
            {
                var newCountriesIds = _context.Countries.Where(x => x.IsActive && !x.IsDeleted).Select(x => x.CountryID).ToList();
                foreach (var id in newCountriesIds)
                {
                    await AnalyzeSingleCountryFull(id);
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in Run Monthly Job", ex);
            }
            
        }

        public async Task RunEvery2HoursJob()
        {
            try
            {
                //await ImportAiScore();
            }
            catch (Exception ex)
            {
               await _appLogger.LogAsync("Error in Running job in Every 2-hour AI ", ex);
            }

        }
        public async Task RunDailyJob()
        {
            try
            {
                await ImportAllCountryImmediateSummary();
                await ImportRemainingDocumentsToVectorDB();
                await DeleteRemainingDocumentsToVectorDB();

            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in Running job in Run daily job ", ex);
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

        public async Task ImportAllCountryImmediateSummary()
        {
            var allCountriesIds = await _context.Countries
                     .Where(x => x.IsActive && !x.IsDeleted)
                     .Select(x => x.CountryID)
                     .ToListAsync();

            foreach (var id in allCountriesIds)
            {
                await AnalyzeCountryImmediateSituation(id);
                await Task.Delay(200);
            }

        }

        public async Task ImportRemainingDocumentsToVectorDB()
        {
            var activeDocumentIds = _context.CountryDocuments
                    .Where(x => !x.IsDeleted)
                    .Select(x => x.CountryDocumentID);

            var data = await _context.DocumentChunks
                .Where(x => !activeDocumentIds.Contains(x.CountryDocumentID))
                .Select(x => x.CountryDocumentID)

                .Union(
                    _context.DocumentTOC
                        .Where(x => !activeDocumentIds.Contains(x.CountryDocumentID))
                        .Select(x => x.CountryDocumentID)
                )
                .Distinct()
                .ToListAsync();


            foreach (var documentID in data)
            {
                await ProcessDocument(documentID);
                await Task.Delay(200);
            }
        }
        public async Task DeleteRemainingDocumentsToVectorDB()
        {
            var activeDocumentIds = _context.CountryDocuments
                    .Where(x => x.IsDeleted)
                    .Select(x => x.CountryDocumentID);

            var data = await _context.DocumentChunks
                .Where(x => activeDocumentIds.Contains(x.CountryDocumentID))
                .Select(x => x.CountryDocumentID)

                .Union(
                    _context.DocumentTOC
                        .Where(x => activeDocumentIds.Contains(x.CountryDocumentID))
                        .Select(x => x.CountryDocumentID)
                )
                .Distinct()
                .ToListAsync();

            foreach (var documentID in data)
            {
                await DeleteDocument(documentID);
                await Task.Delay(200);
            }
        }

        #region Ai api calls       

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
        public async Task AnalyzeCountryImmediateSituation(int countryId)
        {
            var url = aiUrl + AiEndpoints.AnalyzeCountryImmediateSituation(countryId);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }
        public async Task ProcessDocument(int documentID)
        {
            var url = aiUrl + AiEndpoints.ProcessDocument(documentID);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }
        public async Task DeleteDocument(int documentID)
        {
            var url = aiUrl + AiEndpoints.DeleteDocument(documentID);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }
        public async Task<ChatCountryAskQuestionResponse> ChatCountryAsk(ChatCountryAskQuestionRequest request)
        {
            var url = aiUrl + AiEndpoints.ChatCountryAsk();
            var result =  await _httpService.SendAsync<ChatCountryAskQuestionResponse>(HttpMethod.Post, url, request, headers);

            return result;
        }
        public async Task<ChatCountryAskQuestionResponse> ChatGlobalAsk(ChatGlobalAskQuestionRequest request)
        {
                var url = aiUrl + AiEndpoints.ChatGlobalAsk();
                var result = await _httpService.SendAsync<ChatCountryAskQuestionResponse>(HttpMethod.Post, url, request, headers);
    
                return result;
        }
        public async Task<ChatCountryAskQuestionResponse> CrossComparision(CrossComparisionRequest request)
        {
            var url = aiUrl + AiEndpoints.CrossComparision();
            var result = await _httpService.SendAsync<ChatCountryAskQuestionResponse>(HttpMethod.Post, url, request, headers);

            return result;
        }
        public async Task<ChatCountryExecutiveSlidesResponse?> GetCountrySlides(int countryId)
        {
            var url = aiUrl + AiEndpoints.CountrySlides();

            return await _httpService.SendAsync<ChatCountryExecutiveSlidesResponse>(
                HttpMethod.Post,
                url,
                new CountrySlidesRequest
                {
                    CountryId = countryId
                },
                headers
            );
        }
        public async Task AnalyzeCountryMissingQuestions(MissingCountryQuestionRequest r)
        {
            var url = aiUrl + AiEndpoints.AnalyzeCityMissingQuestions();
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, r, headers);
        }

        #endregion Ai api calls 
    }

    #region AiEndpoints

    public static class AiEndpoints
    {
        private const string BasePath = "/api/countries-score-analysis";
        private const string DocumentPath = "/api/rag";
        private const string ChatPath = "/api/chat";

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
        public static string AnalyzeCountryImmediateSituation(int countryId) =>
            $"{BasePath}/analyze/{countryId}/immediateSituation";

        public static string ProcessDocument(int documentId) =>
            $"{DocumentPath}/process-document/{documentId}";
        public static string DeleteDocument(int documentId) =>
            $"{DocumentPath}/delete-document/{documentId}";

        public static string ChatCountryAsk() => $"{ChatPath}/country";
        public static string ChatGlobalAsk() => $"{ChatPath}/global";
        public static string CrossComparision() => $"{ChatPath}/cross-comparision";
        public static string CountrySlides() => $"{ChatPath}/executive-slides";
        public static string AnalyzeCityMissingQuestions() =>
          $"{BasePath}/analyze/missing-pillar-questions";


    }
    #endregion


    #region Ai Models 

    public class MissingCountryQuestionRequest 
    {
        public int CountryID { get; set; }
        public int? PillarID { get; set; }
    }

    public class ChatCountryAskQuestionRequest : ChatGlobalAskQuestionRequest
    {
        public int CountryID { get; set; }
        public int? PillarID { get; set; }
    }
    public class ChatGlobalAskQuestionRequest
    {
        public string QuestionText { get; set; }
        public string? HistoryText { get; set; }
        public int? FAQID { get; set; }
    }

    public class CrossComparisionRequest
    {
        public List<int> CountryIDs { get; set; }
        public string QuestionText { get; set; }
        public string? HistoryText { get; set; }
    }
    public class ChatCountryAskQuestionResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Result { get; set; }
    }

    #endregion
}
