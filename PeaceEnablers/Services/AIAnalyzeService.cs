using Microsoft.EntityFrameworkCore;
using PeaceEnablers.AI;
using PeaceEnablers.AI.Endpoints;
using PeaceEnablers.Common.Interface;
using PeaceEnablers.Data;
using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Dtos.chatDto;
using PeaceEnablers.IServices;

namespace PeaceEnablers.Services
{
    public class AIAnalyzeService : IAIAnalyzeService
    {
        #region constructor

        private readonly IAiGateway _aiGateway;
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly ICommonService _commonService;

        public AIAnalyzeService(
            IAiGateway aiGateway,
            ApplicationDbContext context,
            IAppLogger appLogger,
            ICommonService commonService)
        {
            _aiGateway = aiGateway;
            _context = context;
            _appLogger = appLogger;
            _commonService = commonService;
        }

        #endregion

        #region RunMonthlyJob

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
                await _appLogger.LogAsync(AiMessages.Log.MonthlyJobFailed, ex);
            }
        }

        #endregion

        #region RunEvery2HoursJob

        public async Task RunEvery2HoursJob()
        {
            try
            {
                //await ImportAiScore();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync(AiMessages.Log.TwoHourJobFailed, ex);
            }
        }

        #endregion

        #region RunDailyJob

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
                await _appLogger.LogAsync(AiMessages.Log.DailyJobFailed, ex);
            }
        }

        #endregion

        #region ImportAiScore

        public async Task ImportAiScore()
        {
            var totalPillar = (await _commonService.GetPillars()).Count;
            var allCountriesIds = _context.Countries.Where(x => x.IsActive && !x.IsDeleted).Select(x => x.CountryID).ToList();
            var importedCountriesIds = _context.AICountryScores.Select(x => x.CountryID);

            var newCountriesIds = allCountriesIds.Where(x => !importedCountriesIds.Contains(x)).ToList();
            foreach (var id in newCountriesIds)
            {
                await AnalyzeSingleCountryFull(id);
            }

            var now = DateTime.UtcNow;

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

            var needtoImportcountryIds = _context.AICountryScores.Where(x => x.UpdatedAt < date).Select(x => x.CountryID);
            foreach (var id in needtoImportcountryIds)
            {
                await AnalyzeSingleCountry(id);
            }
        }

        #endregion

        #region ImportAllCountryImmediateSummary

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

        #endregion

        #region ImportRemainingDocumentsToVectorDB

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

        #endregion

        #region DeleteRemainingDocumentsToVectorDB

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

        #endregion

        #region AnalyzeAllCountriesFull

        public async Task AnalyzeAllCountriesFull()
        {
            await _aiGateway.ExecuteAsync(HttpMethod.Post, EvaluationAiEndpoints.AnalyzeAllCountriesFull);
        }

        #endregion

        #region AnalyzeSingleCountryFull

        public async Task AnalyzeSingleCountryFull(int countryId)
        {
            await _aiGateway.ExecuteAsync(HttpMethod.Post, EvaluationAiEndpoints.AnalyzeSingleCountryFull(countryId));
        }

        #endregion

        #region AnalyzeSingleCountry

        public async Task AnalyzeSingleCountry(int countryId)
        {
            await _aiGateway.ExecuteAsync(HttpMethod.Post, EvaluationAiEndpoints.AnalyzeSingleCountry(countryId));
        }

        #endregion

        #region AnalyzeCountryPillars

        public async Task AnalyzeCountryPillars(int countryId)
        {
            await _aiGateway.ExecuteAsync(HttpMethod.Post, EvaluationAiEndpoints.AnalyzeCountryPillars(countryId));
        }

        #endregion

        #region AnalyzeSinglePillar

        public async Task AnalyzeSinglePillar(int countryId, int pillarId)
        {
            await _aiGateway.ExecuteAsync(HttpMethod.Post, EvaluationAiEndpoints.AnalyzeSinglePillar(countryId, pillarId));
        }

        #endregion

        #region AnalyzeQuestionsOfCountry

        public async Task AnalyzeQuestionsOfCountry(int countryId)
        {
            await _aiGateway.ExecuteAsync(HttpMethod.Post, EvaluationAiEndpoints.AnalyzeCountryQuestions(countryId));
        }

        #endregion

        #region AnalyzeQuestionsOfCountryPillar

        public async Task AnalyzeQuestionsOfCountryPillar(int countryId, int pillarId)
        {
            await _aiGateway.ExecuteAsync(HttpMethod.Post, EvaluationAiEndpoints.AnalyzeCountryPillarQuestions(countryId, pillarId));
        }

        #endregion

        #region AnalyzeCountryImmediateSituation

        public async Task AnalyzeCountryImmediateSituation(int countryId)
        {
            await _aiGateway.ExecuteAsync(HttpMethod.Post, EvaluationAiEndpoints.AnalyzeCountryImmediateSituation(countryId));
        }

        #endregion

        #region ProcessDocument

        public async Task ProcessDocument(int documentID)
        {
            await _aiGateway.ExecuteAsync(HttpMethod.Post, RagAiEndpoints.ProcessDocument(documentID));
        }

        #endregion

        #region DeleteDocument

        public async Task DeleteDocument(int documentID)
        {
            await _aiGateway.ExecuteAsync(HttpMethod.Post, RagAiEndpoints.DeleteDocument(documentID));
        }

        #endregion

        #region ChatCountryAsk

        public async Task<ChatCountryAskQuestionResponse> ChatCountryAsk(ChatCountryAskQuestionRequest request)
        {
            var call = await _aiGateway.SendAsync<ChatCountryAskQuestionResponse>(
                HttpMethod.Post,
                ChatAiEndpoints.CountryAsk,
                request);

            return call.ToEnvelope(() => new ChatCountryAskQuestionResponse());
        }

        #endregion

        #region ChatGlobalAsk

        public async Task<ChatCountryAskQuestionResponse> ChatGlobalAsk(ChatGlobalAskQuestionRequest request)
        {
            var call = await _aiGateway.SendAsync<ChatCountryAskQuestionResponse>(
                HttpMethod.Post,
                ChatAiEndpoints.GlobalAsk,
                request);

            return call.ToEnvelope(() => new ChatCountryAskQuestionResponse());
        }

        #endregion

        #region CrossComparision

        public async Task<ChatCountryAskQuestionResponse> CrossComparision(CrossComparisionRequest request)
        {
            var call = await _aiGateway.SendAsync<ChatCountryAskQuestionResponse>(
                HttpMethod.Post,
                ChatAiEndpoints.CrossComparision,
                request);

            return call.ToEnvelope(() => new ChatCountryAskQuestionResponse());
        }

        #endregion

        #region GetCountrySlides

        public async Task<ChatCountryExecutiveSlidesResponse?> GetCountrySlides(int countryId)
        {
            var call = await _aiGateway.SendAsync<ChatCountryExecutiveSlidesResponse>(
                HttpMethod.Post,
                ChatAiEndpoints.CountrySlides,
                new CountrySlidesRequest
                {
                    CountryId = countryId
                });

            return call.ToEnvelope(() => new ChatCountryExecutiveSlidesResponse());
        }

        #endregion

        #region GetEmergingTrendsAndIssues

        public async Task<ChatEmergingTrendsResponse?> GetEmergingTrendsAndIssues(int countryCount)
        {
            var call = await _aiGateway.SendAsync<ChatEmergingTrendsResponse>(
                HttpMethod.Get,
                ChatAiEndpoints.EmergingTrendsAndIssues(countryCount));

            return call.ToEnvelope(() => new ChatEmergingTrendsResponse());
        }

        #endregion

        #region GetPillarLiveSignals

        public async Task<ChatPillarLiveSignalsResponse?> GetPillarLiveSignals()
        {
            var call = await _aiGateway.SendAsync<ChatPillarLiveSignalsResponse>(
                HttpMethod.Get,
                ChatAiEndpoints.PillarLiveSignals);

            return call.ToEnvelope(() => new ChatPillarLiveSignalsResponse());
        }

        #endregion

        #region AnalyzeCountryMissingQuestions

        public async Task AnalyzeCountryMissingQuestions(MissingCountryQuestionRequest r)
        {
            await _aiGateway.ExecuteAsync(HttpMethod.Post, EvaluationAiEndpoints.AnalyzeCityMissingQuestions, r);
        }

        #endregion

        #region SummarizeKpiPerformance

        public async Task<KpiSummaryAiResponse?> SummarizeKpiPerformance(KpiSummaryAiRequest request)
        {
            var call = await _aiGateway.SendAsync<KpiSummaryAiResponse>(
                HttpMethod.Post,
                ChatAiEndpoints.KpiSummary,
                request);

            return call.ToEnvelope(() => new KpiSummaryAiResponse());
        }

        #endregion
    }
}
