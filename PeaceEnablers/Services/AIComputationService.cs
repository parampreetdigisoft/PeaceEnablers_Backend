using AssessmentPlatform.Dtos.AiDto;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PeaceEnablers.Backgroundjob;
using PeaceEnablers.Common.Implementation;
using PeaceEnablers.Common.Interface;
using PeaceEnablers.Common.Models;
using PeaceEnablers.Common.Models.settings;
using PeaceEnablers.Data;
using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Dtos.CountryDto;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Net;
using System.Text.RegularExpressions;

namespace PeaceEnablers.Services
{
    public class AIComputationService : IAIComputationService
    {
        #region constructor
        
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly ICommonService _commonService;
        private readonly Download _download;
        private readonly IAIAnalyzeService _iAIAnalayzeService;        
        private readonly IDocumentGeneratorService _documentGeneratorService;
        private readonly AppSettings _appSettings;
        public AIComputationService(ApplicationDbContext context, IAppLogger appLogger, ICommonService commonService, Download download, IAIAnalyzeService iAIAnalayzeService
            ,  IDocumentGeneratorService documentGeneratorService, IOptions<AppSettings> appSettings)
        {
            _context = context;
            _appLogger = appLogger;
            _commonService = commonService;
            _download = download;
            _iAIAnalayzeService = iAIAnalayzeService;          
            _documentGeneratorService = documentGeneratorService;
            _appSettings = appSettings.Value;
        }
        #endregion

        #region implementation

     
        public async Task<ResultResponseDto<List<AITrustLevel>>> GetAITrustLevels()
        {
            var r = await _context.AITrustLevels.ToListAsync();

            return ResultResponseDto<List<AITrustLevel>>.Success(r, new[] { "Pillar get successfully" });

        }
        public async Task<PaginationResponse<AiCountrySummeryDto>> GetAICountries(AiCountrySummeryRequestDto request, int userID, UserRole userRole)
        {
            try
            {
                IQueryable<AiCountrySummeryDto> query = await GetCountryAiSummeryDetails(userID, userRole, request.CountryID, request.Year);

                var result = await query.ApplyPaginationAsync(request);
                int pillarCount = _appSettings.PillarCount;

                if (userRole != UserRole.CountryUser)
                {
                    var progress = await _commonService.GetCountriesProgressAsync(userID, (int)userRole, DateTime.Now.Year);

                    var ids = result.Data.Select(x => x.CountryID);
                    var countries = progress.Where(x => ids.Contains(x.CountryID));


                    var counts = await _context.Pillars
                        .Select(p => p.Questions.Count()).ToListAsync();

                    var totalQuestions = counts.Sum();

                    var answeredQuestions = await _context.AIEstimatedQuestionScores
                        .Where(x => x.Year == request.Year && ids.Contains(x.CountryID))
                        .GroupBy(x => x.CountryID)
                        .Select(g => new
                        {
                            CountryID = g.Key,
                            CompletionRate = totalQuestions == 0
                                ? 0
                                : g.Count() * 100.0M / totalQuestions
                        })
                        .ToListAsync();

                    foreach (var c in result.Data)
                    {
                        var pillars = countries.Where(x => x.CountryID == c.CountryID);
                        var countryScore = Math.Round(pillars.Sum(x => x.ScoreProgress) / pillarCount, 2);
                        c.EvaluatorScore = countryScore;
                        c.Discrepancy = Math.Abs(countryScore - (c.AIProgress ?? 0));
                        c.AICompletionRate = answeredQuestions.FirstOrDefault(x=>x.CountryID== c.CountryID)?.CompletionRate;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetCountriesAsync", ex);
                return new PaginationResponse<AiCountrySummeryDto>();
            }
        }
        public async Task<IQueryable<AiCountrySummeryDto>> GetCountryAiSummeryDetails(int userID, UserRole userRole, int? countryID, int currentYear=0)
        {
            currentYear = currentYear ==0 ? DateTime.Now.Year : currentYear;
            var firstDate = new DateTime(currentYear, 1, 1); 
            var endDate = new DateTime(currentYear+1, 1, 1); 
            IQueryable<AICountryScore> baseQuery = _context.AICountryScores.Where(x=> x.UpdatedAt >= firstDate && x.UpdatedAt < endDate && x.Year== currentYear);

            List<int> allowedCountryIds = new();
            if (userRole == UserRole.Analyst)
            {
                // Allowed country IDs
                allowedCountryIds = await _context.UserCountryMappings
                            .Where(x => !x.IsDeleted && x.UserID == userID && (!countryID.HasValue || x.CountryID == countryID.Value))
                            .Select(x => x.CountryID)
                            .Distinct()
                            .ToListAsync();

                baseQuery = baseQuery.Where(x => allowedCountryIds.Contains(x.CountryID));
            }
            else if (userRole == UserRole.Evaluator)
            {
                // Allowed country IDs
                allowedCountryIds = await _context.AIUserCountryMappings
                            .Where(x => x.IsActive && x.UserID == userID && (!countryID.HasValue || x.CountryID == countryID.Value))
                            .Select(x => x.CountryID)
                            .Distinct()
                            .ToListAsync();

                baseQuery = baseQuery.Where(x => allowedCountryIds.Contains(x.CountryID));
            }
            else if (userRole == UserRole.CountryUser)
            {
                allowedCountryIds = await _context.PublicUserCountryMappings
                            .Where(x => x.IsActive && x.UserID == userID && (!countryID.HasValue || x.CountryID == countryID.Value))
                            .Select(x => x.CountryID)
                            .Distinct()
                            .ToListAsync();

                baseQuery = baseQuery.Where(x => allowedCountryIds.Contains(x.CountryID) && x.IsVerified);
            }
            else
            {
                // Admin
                if (countryID.HasValue)
                {
                    baseQuery = baseQuery.Where(x => x.CountryID == countryID.Value);
                    allowedCountryIds = new() { countryID.Value };
                }
            }
            var commentQuery = _context.AIUserCountryMappings
                .Where(x =>
                    (
                        userRole == UserRole.Admin ||
                        (userRole == UserRole.Analyst && x.AssignBy == userID) ||
                        (userRole == UserRole.Evaluator && x.UserID == userID)
                    )
                )
                .GroupBy(x => x.CountryID)
                .Select(g => new
                {
                    CountryID = g.Key,
                    Comment = g
                        .OrderByDescending(x => x.UpdatedAt >= firstDate && x.UpdatedAt < endDate)
                        .Select(x => x.Comment)
                        .FirstOrDefault()
                });

            var query =
                from c in _context.Countries
                where !c.IsDeleted && (allowedCountryIds.Contains(c.CountryID) || (userRole == UserRole.Admin && !countryID.HasValue))
                join score in baseQuery
                    on c.CountryID equals score.CountryID
                    into scoreJoin
                from score in scoreJoin.DefaultIfEmpty()   // LEFT JOIN score

                join cmt in commentQuery
                    on c.CountryID equals cmt.CountryID
                    into cmtJoin
                from cmt in cmtJoin.DefaultIfEmpty()       // LEFT JOIN comment

                select new AiCountrySummeryDto
                {
                    CountryID = c.CountryID,
                    Continent = c.Continent ?? string.Empty,
                    CountryName = c.CountryName ?? string.Empty,
                    //Country = c.Country ?? string.Empty,
                    Image = c.Image ?? string.Empty,

                    Year = score != null ? score.Year : currentYear,
                    AIScore = score != null ? score.AIScore : null,
                    AIProgress = score != null ? score.AIProgress : null,
                    EvaluatorScore = score != null ? score.EvaluatorScore : null,
                    Discrepancy = score != null ? score.Discrepancy : null,

                    ConfidenceLevel = score != null ? score.ConfidenceLevel ?? string.Empty : string.Empty,
                    EvidenceSummary = score != null ? score.EvidenceSummary ?? string.Empty : string.Empty,

                    StructuralEvidence = score != null ? score.StructuralEvidence : null,
                    OperationalEvidence = score != null ? score.OperationalEvidence : null,
                    OutcomeEvidence = score != null ? score.OutcomeEvidence : null,
                    PerceptionEvidence = score != null ? score.PerceptionEvidence : null,

                    TemporalScope = score != null ? score.TemporalScope : null,
                    DistortionScreening = score != null ? score.DistortionScreening : null,

                    PoliticalShock = score != null ? score.PoliticalShock : null,
                    EconomicShock = score != null ? score.EconomicShock : null,
                    NarrativeShock = score != null ? score.NarrativeShock : null,

                    OverallStressResilience = score != null ? score.OverallStressResilience : null,
                    StressScoreAdjustment = score != null ? score.StressScoreAdjustment : null,
                    InequalityAdjustment = score != null ? score.InequalityAdjustment : null,
                    OpacityRisk = score != null ? score.OpacityRisk : null,
                    NonCompensationNote = score != null ? score.NonCompensationNote : null,

                    CrossPillarPatterns = score != null ? score.CrossPillarPatterns : null,
                    RelationalIntegrity = score != null ? score.RelationalIntegrity : null,
                    InstitutionalCapacity = score != null ? score.InstitutionalCapacity : null,
                    EquityAssessment = score != null ? score.EquityAssessment : null,
                    ConflictRiskOutlook = score != null ? score.ConflictRiskOutlook : null,

                    StrategicRecommendation = score != null ? score.StrategicRecommendation : null,
                    DataTransparencyNote = score != null ? score.DataTransparencyNote : null,
                    PrimarySource = score != null ? score.PrimarySource : null,

                    UpdatedAt = score != null ? score.UpdatedAt : default(DateTime),

                    IsVerified = score != null && score.IsVerified
                };
            return query;
        }
    
        public async Task<ResultResponseDto<AiCountryPillarResponseDto>> GetAICountryPillars(int CountryID, int userID, UserRole userRole, int currentYear = 0)
        {
            try
            {
                currentYear = currentYear == 0 ? DateTime.Now.Year : currentYear;
                var firstDate = new DateTime(currentYear, 1, 1);
                int pillarCount = _appSettings.PillarCount;
                var res = await _context.AIPillarScores
                    .Where(x => x.CountryID == CountryID && x.UpdatedAt >= firstDate && x.Year == currentYear)
                    .Include(x=>x.Country)
                    .Include(x => x.DataSourceCitations)
                    .ToListAsync();

                List<int> pillarIds = new();
                if (userRole == UserRole.CountryUser)
                {
                    pillarIds = await _context.CountryUserPillarMappings
                                .Where(x => x.IsActive && x.UserID == userID)
                                .Select(x => x.PillarID)
                                .Distinct()
                                .ToListAsync();
                }
                var pillars = await _context.Pillars.Select(x=>new
                {
                    PillarID = x.PillarID,
                    PillarName = x.PillarName,
                    DisplayOrder = x.DisplayOrder,
                    ImagePath = x.ImagePath,
                    TotalQuestions = x.Questions.Count()
                }).ToListAsync();

                var result = pillars
                .GroupJoin(
                    res,
                    p => p.PillarID,
                    s => s.PillarID,
                    (pillar, scores) => new { pillar, score = scores.FirstOrDefault() }
                )
                .Select(x =>
                {
                    var isAccess = pillarIds.Count == 0 || pillarIds.Contains(x.pillar.PillarID);

                    var r = new AiCountryPillarResponse
                    {
                        PillarScoreID = x.score?.PillarScoreID ?? 0,
                        CountryID = x.score?.CountryID ?? CountryID,
                        CountryName = x.score?.Country?.CountryName ?? "",
                        Continent = x.score?.Country?.Continent ?? "",                        
                        PillarID = x.pillar.PillarID,
                        PillarName = x.pillar.PillarName,
                        DisplayOrder = x.pillar.DisplayOrder,
                        ImagePath = x.pillar.ImagePath,
                        IsAccess = isAccess
                    };

                    if (isAccess && x.score != null)
                    {
                        r.AIDataYear = x.score.Year;
                        r.AIScore = x.score.AIScore;
                        r.AIProgress = x.score.AIProgress;
                        r.EvaluatorScore = x.score.EvaluatorScore;
                        r.Discrepancy = x.score.Discrepancy;
                        r.ConfidenceLevel = x.score.ConfidenceLevel;
                        r.EvidenceSummary = x.score.EvidenceSummary;
                        r.StructuralEvidence = x.score.StructuralEvidence;
                        r.OperationalEvidence = x.score.OperationalEvidence;
                        r.OutcomeEvidence = x.score.OutcomeEvidence;
                        r.PerceptionEvidence = x.score.PerceptionEvidence;
                        r.TemporalScope = x.score.TemporalScope;
                        r.DistortionScreening = x.score.DistortionScreening;
                        r.RelationalIntegrity = x.score.RelationalIntegrity;
                        r.StressPoliticalShock = x.score.StressPoliticalShock;
                        r.StressEconomicShock = x.score.StressEconomicShock;
                        r.StressNarrativeShock = x.score.StressNarrativeShock;
                        r.StressOverallResilience = x.score.StressOverallResilience;
                        r.StressScoreAdjustment = x.score.StressScoreAdjustment;
                        r.InequalityAdjustment = x.score.InequalityAdjustment;
                        r.OpacityRisk = x.score.OpacityRisk;
                        r.NonCompensationNote = x.score.NonCompensationNote;
                        r.GeographicEquityNote = x.score.GeographicEquityNote;
                        r.InstitutionalAssessment = x.score.InstitutionalAssessment;
                        r.DataGapAnalysis = x.score.DataGapAnalysis;
                        r.RedFlag = x.score.RedFlag;
                        r.DataSourceCitations = x.score.DataSourceCitations;
                        r.UpdatedAt = x.score.UpdatedAt;
                    }
                    return r;
                })
                .OrderBy(x => !x.IsAccess)
                .ThenBy(x => x.DisplayOrder)
                .ToList();


                var progress = await _commonService.GetCountriesProgressAsync(userID, (int)userRole, currentYear);

                var countries = progress.Where(x => x.CountryID== CountryID);

                var answeredQuestions = await _context.AIEstimatedQuestionScores
               .Where(x => x.Year == currentYear && x.CountryID == CountryID)
               .GroupBy(x => x.PillarID)
               .Select(g => new
               {
                   PillarID = g.Key,
                   AnsweredQuestions = g.Count() 
               })
               .ToListAsync();

                foreach (var c in result)
                {
                    var totalQuestions = pillars.FirstOrDefault(x => x.PillarID == c.PillarID)?.TotalQuestions ?? 0;
                    var answeredQuestion = answeredQuestions.FirstOrDefault(x => x.PillarID == c.PillarID)?.AnsweredQuestions ?? 0;
                    var pillarScore = countries
                        .Where(x => x.PillarID == c.PillarID)
                        .Select(x => x.ScoreProgress)
                        .DefaultIfEmpty(0)
                        .Sum();
                    c.EvaluatorScore = pillarScore;
                    c.Discrepancy = Math.Abs(pillarScore - (c.AIProgress ?? 0));
                    c.AICompletionRate = answeredQuestion * 100.0M / totalQuestions;
                }

                var finalResutl = new AiCountryPillarResponseDto
                {

                    Pillars = result
                };

                var resposne = ResultResponseDto<AiCountryPillarResponseDto>.Success(finalResutl, new[] { "Pillar get successfully", });

                return resposne;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetAICityPillars", ex);
                return ResultResponseDto<AiCountryPillarResponseDto>.Failure(new[] { "Error in getting pillar details", });
            }
        }
        public async Task<PaginationResponse<AIEstimatedQuestionScoreDto>> GetAIPillarsQuestion(AiCountryPillarSummeryRequestDto request, int userID, UserRole userRole)
        {
            try
            {
                if (userRole == UserRole.CountryUser && request.CountryID != null && request.PillarID != null)
                {
                    var isPillarAccess = _context.CountryUserPillarMappings
                                .Where(x => x.IsActive && x.UserID == userID)
                                .Select(x => x.PillarID).Contains(request.PillarID.Value);

                    var isCityAccess = _context.PublicUserCountryMappings
                               .Where(x => x.IsActive && x.UserID == userID)
                               .Select(x => x.CountryID).Contains(request.CountryID.Value);
                    if (!(isCityAccess && isPillarAccess))
                    {
                        return new PaginationResponse<AIEstimatedQuestionScoreDto>();
                    }
                }
                var currentYear = request.Year;
                var firstDate = new DateTime(currentYear, 1, 1);

                var res =
                    from q in _context.Questions.Where(x=>x.PillarID== request.PillarID)
                    join s in _context.AIEstimatedQuestionScores
                        .Where(x =>
                            x.CountryID == request.CountryID &&
                            x.PillarID == request.PillarID &&
                            x.UpdatedAt >= firstDate && x.Year == currentYear)
                    on q.QuestionID equals s.QuestionID into qs
                    from x in qs.DefaultIfEmpty() // LEFT JOIN
                    select new AIEstimatedQuestionScoreDto
                    {
                        CountryID = x == null ? request.CountryID ?? 0 : x.CountryID,
                        PillarID = x == null ? request.PillarID ?? 0 : x.PillarID,
                        QuestionID = q.QuestionID,
                        Year = x == null ? currentYear : x.Year,
                        AIScore = x == null ? null : x.AIScore,
                        AIProgress = x == null ? null : x.AIProgress,
                        EvaluatorScore = x == null ? null : x.EvaluatorScore,
                        Discrepancy = x == null ? null : x.Discrepancy,
                        ConfidenceLevel = x == null ? string.Empty : x.ConfidenceLevel,
                        SourcesConsulted = x == null ? null : x.SourcesConsulted,  // ✅ renamed
                        EvidenceSummary = x == null ? string.Empty : x.EvidenceSummary,
                        // Evidence Dimensions
                        StructuralEvidence = x == null ? string.Empty : x.StructuralEvidence,
                        OperationalEvidence = x == null ? string.Empty : x.OperationalEvidence,
                        OutcomeEvidence = x == null ? string.Empty : x.OutcomeEvidence,
                        PerceptionEvidence = x == null ? string.Empty : x.PerceptionEvidence,
                        TemporalScope = x == null ? string.Empty : x.TemporalScope,
                        DistortionScreening = x == null ? string.Empty : x.DistortionScreening,
                        RelationalDependencies = x == null ? string.Empty : x.RelationalDependencies,
                        // Stress Tests
                        StressPoliticalShock = x == null ? string.Empty : x.StressPoliticalShock,
                        StressEconomicShock = x == null ? string.Empty : x.StressEconomicShock,
                        StressNarrativeShock = x == null ? string.Empty : x.StressNarrativeShock,
                        StressOverallResilienceShock = x == null ? string.Empty : x.StressOverallResilienceShock,
                        InequalityAdjustment = x == null ? string.Empty : x.InequalityAdjustment,   // ✅ renamed
                        OpacityRisk = x == null ? string.Empty : x.OpacityRisk,
                        RedFlag = x == null ? string.Empty : x.RedFlag,   // ✅ renamed
                        // Source Metadata
                        SourceType = x == null ? string.Empty : x.SourceType,
                        SourceName = x == null ? string.Empty : x.SourceName,
                        SourceURL = x == null ? string.Empty : x.SourceURL,
                        SourceDataExtract = x == null ? string.Empty : x.SourceDataExtract,
                        SourceDataYear = x == null ? null : x.SourceDataYear,
                        SourceHierarchyLevel = x == null ? null : x.SourceHierarchyLevel,   // ✅ renamed
                        UpdatedAt = x == null ? null : x.UpdatedAt,

                        QuestionText = q.QuestionText ?? string.Empty
                    };

                var r = await res.ApplyPaginationAsync(request);

                return r;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetAICountryPillars", ex);
                return new PaginationResponse<AIEstimatedQuestionScoreDto>();
            }
        }        
        private async Task<List<PeerCountryHistoryReportDto>> GetPeerCountries(int userID, UserRole role, int CountryID, int year, bool isAiScore = true)
        {
            var peerCountries = new List<PeerCountryHistoryReportDto>();
            int pillarCount = _appSettings.PillarCount;
            var peersCountryIDs = await _context.Countries
                   .Where(x => x.CountryID == CountryID && x.IsActive && !x.IsDeleted)
                   .SelectMany(x => x.CountryPeers)
                   .Where(x => x.IsActive && !x.IsDeleted)
                   .Select(x => x.PeerCountryID)
                   .ToListAsync();
            if (peersCountryIDs.Count > 0)
            {
                peersCountryIDs.Add(CountryID);
            }

            var startYear = year - 5;

            peerCountries = await _context.Countries
                .Where(c => peersCountryIDs.Contains(c.CountryID))
                .Select(c => new PeerCountryHistoryReportDto
                {
                    CountryID = c.CountryID,
                    CountryName = c.CountryName,
                    Continent = c.Continent,
                    Country = c.CountryName,
                    Region = c.Region,
                    CountryCode = c.CountryCode,
                    UpdatedDate = c.UpdatedDate,
                    Image = c.Image,
                    Latitude = c.Latitude,
                    Longitude = c.Longitude,
                    Population = c.Population,
                    Income = c.Income                  

                }).ToListAsync();

            if (isAiScore)
            {
                foreach (var c in peerCountries)
                {
                    c.CountryHistory = _context.AIPillarScores
                    .Include(x => x.Pillar)
                    .Where(x =>
                        x.CountryID == c.CountryID &&
                        x.Year >= startYear &&
                        x.Year <= year)
                    .GroupBy(x => x.Year)
                    .Select(yearGroup => new PeerCountryYearHistoryDto
                    {
                        CountryID = c.CountryID,
                        Year = yearGroup.Key,

                        ScoreProgress = yearGroup.Average(x => x.AIProgress ?? 0),

                        Pillars = yearGroup
                            .GroupBy(p => new
                            {
                                p.PillarID,
                                p.Pillar.PillarName,
                                p.Pillar.DisplayOrder
                            })
                            .Select(pillarGroup => new PeerCountryPillarHistoryReportDto
                            {
                                PillarID = pillarGroup.Key.PillarID,
                                PillarName = pillarGroup.Key.PillarName,
                                DisplayOrder = pillarGroup.Key.DisplayOrder,
                                ScoreProgress = pillarGroup.Average(x => x.AIProgress ?? 0)
                            })
                            .OrderBy(x => x.DisplayOrder)
                            .ToList()
                    })
                    .OrderBy(x => x.Year)
                    .ToList();
                }
            }
            else
            {
                var pillars = await _context.Pillars.Select(x => new
                {
                    x.PillarID,
                    x.PillarName,
                    x.DisplayOrder
                }).ToListAsync();

                var countryProgress = await _commonService
                    .GetCountriesProgressHistoryAsync(userID, (int)role, year - 5, year);

                var filterCountries = countryProgress
                    .Where(x => peersCountryIDs.Contains(x.CountryID))
                    .ToList();

                foreach (var country in peerCountries)
                {
                    var progress = filterCountries
                        .Where(x => x.CountryID == country.CountryID)
                        .ToList();

                    // ✅ Build Year-wise history first
                    country.CountryHistory = progress
                        .GroupBy(x => x.Year)
                        .Select(yearGroup => new PeerCountryYearHistoryDto
                        {
                            CountryID = country.CountryID,
                            Year = yearGroup.Key,

                            // City level score
                            ScoreProgress = Math.Round(
                                yearGroup.Select(x => x.ScoreProgress)
                                         .DefaultIfEmpty(0)
                                         .Sum()/pillarCount, 2),

                            // Pillar level score
                            Pillars = pillars
                                .Select(p => new PeerCountryPillarHistoryReportDto
                                {
                                    PillarID = p.PillarID,
                                    PillarName = p.PillarName,
                                    DisplayOrder = p.DisplayOrder,

                                    ScoreProgress = Math.Round(
                                        yearGroup
                                            .Where(x => x.PillarID == p.PillarID)
                                            .Select(x => x.ScoreProgress)
                                            .DefaultIfEmpty(0)
                                            .Average(), 2)
                                })
                                .OrderBy(x => x.DisplayOrder)
                                .ToList()
                        })
                        .OrderBy(x => x.Year)
                        .ToList();
                }
            }

            return peerCountries;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  ENTRY POINTS  (GeneratecountryDetailsPdf / GeneratePillarDetailsPdf)
        // ─────────────────────────────────────────────────────────────────────────────

        public async Task<byte[]> GenerateCountryDetailsReport(AiCountrySummeryDto countryDetails, UserRole userRole, int userID,
           IServices.DocumentFormat format = IServices.DocumentFormat.Pdf, string reportType = "ai")
        {
            try
            {
                var isManual = reportType != "ai" && userRole == UserRole.Admin ? true : false;

                var pillars = await GetAICountryPillars(countryDetails.CountryID, userID, userRole, countryDetails.Year);

                var kpis = await GetAccessKpis(userID, userRole, countryDetails.CountryID, countryDetails.Year, !isManual);

                if (isManual)
                {
                    countryDetails.AIProgress = countryDetails.EvaluatorScore;

                    foreach (var pillar in pillars.Result.Pillars)
                    {
                        pillar.AIProgress = pillar.EvaluatorScore;
                    }
                }

                var peerCountries = await GetPeerCountries(userID, userRole, countryDetails.CountryID, countryDetails.Year, !isManual);


                var document = await _documentGeneratorService.GenerateCountryDetails(countryDetails, pillars.Result.Pillars, kpis, peerCountries, userRole, format);

                return document;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GenerateCountryDetailsReport", ex);
                return Array.Empty<byte>();
            }
        }
        public async Task<byte[]> GeneratePillarDetailsReport(AiCountryPillarResponse pillarData, UserRole userRole, IServices.DocumentFormat format = IServices.DocumentFormat.Pdf)
        {
            try
            {
                var document = await _documentGeneratorService.GeneratePillarDetails(pillarData, userRole, format);


                return document;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GeneratePillarDetailsReport", ex);
                return Array.Empty<byte>();
            }
        }

        void PillarProgressBar(ColumnDescriptor column, string label, decimal? percentage, string color)
        {
            var per = (float)(percentage ?? 0);
            column.Item().Row(row =>
            {
                row.ConstantItem(140).Text(label)                                                                                                   
                    .FontSize(11)                                                                                                               
                    .FontColor("#424242");
                if (per > 0)
                    row.RelativeItem().PaddingLeft(10).Column(col =>
                    {
                        col.Item().Height(20).Background("#F5F5F5").Row(barRow =>
                        {
                            barRow.RelativeItem(per).Background(color);
                            barRow.RelativeItem(100 - (per==100? 99.9f: per));
                        });
                    });

                row.ConstantItem(55).AlignRight().Text($"{percentage:F1}%")
                    .FontSize(11)
                    .Bold()
                    .FontColor(color);
            });
        }
        
        void PillarComposeFooter(IContainer container)
        {
            container.AlignCenter().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().AlignCenter().Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();

                    });


                    col.Item().PaddingTop(5).AlignCenter().Text("AI Power Country Assessment Platform")
                        .FontSize(8)
                        .FontColor("#9E9E9E");
                });
            });
        }
        static string GetConfidenceBadgeColor(string confidence) => confidence?.ToLower() switch
        {
            "high" => "#44826c",
            "medium" => "#FFC107",
            "low" => "#F44336",
            _ => "#9E9E9E"
        };
        static string GetDiscrepancyColor(decimal discrepancy) => discrepancy switch
        {
            < 10 => "#4a754c",
            < 25 => "#FFC107",
            _ => "#F44336"
        };
        static string GetSourceTypeBadgeColor(string sourceType) => sourceType?.ToLower() switch
        {
            "government" => "#133328",
            "academic" => "#172923",
            "international" => "#4d7d6d",
            "news/ngo" => "#1ec990",
            _ => "#0eeba1"
        };
        static string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
            return text.Substring(0, maxLength) + "...";
        }
        public async Task<ResultResponseDto<AiCrossCountryResponseDto>> GetAICrossCountryPillars(AiCountryIdsDto CountryIDs, int userID, UserRole userRole)
        {
            try
            {
                var currentYear = DateTime.Now.Year;
                var response = new AiCrossCountryResponseDto();

                var firstDate = new DateTime(currentYear, 1, 1);

                var aiPillarScores = await _context.AIPillarScores
                    .Where(x => CountryIDs.CountryIDs.Contains(x.CountryID) && x.UpdatedAt >= firstDate)
                    .ToListAsync();

                var countries = await _context.Countries
                    .Where(x => CountryIDs.CountryIDs.Contains(x.CountryID))
                    .ToListAsync();

                // Pillar access based on role
                List<int> pillarIds = new();
                if (userRole == UserRole.CountryUser)
                {
                    pillarIds = await _context.CountryUserPillarMappings
                        .Where(x => x.IsActive && x.UserID == userID)
                        .Select(x => x.PillarID)
                        .Distinct()
                        .ToListAsync();
                }

                var pillars = await _context.Pillars.ToListAsync();

                // Categories
                response.Categories.AddRange(
                    pillars
                        .Where(x => pillarIds.Count == 0 || pillarIds.Contains(x.PillarID))
                        .OrderBy(x=>x.DisplayOrder)
                        .Select(x => x.PillarName)
                );
                // Per country processing

                var aiCountries = await _context.AICountryScores
                    .Where(x => CountryIDs.CountryIDs.Contains(x.CountryID) &&
                                x.Year == currentYear && ((userRole == UserRole.CountryUser && x.IsVerified) || userRole != UserRole.CountryUser))
                    .GroupBy(x => x.CountryID)
                    .Select(g => new
                    {
                        CountryID = g.Key,
                        AIProgress = g.Max(x => x.AIProgress)
                    })
                    .ToDictionaryAsync(x => x.CountryID, x => x.AIProgress);


                foreach (var country in countries)
                {
                    var pillarResults = pillars
                    .GroupJoin(
                        aiPillarScores.Where(x => x.CountryID == country.CountryID),
                        p => p.PillarID,
                        s => s.PillarID,
                        (pillar, scores) => new
                        {
                            Pillar = pillar,
                            Score = scores.FirstOrDefault()
                        })
                    .Select(x =>
                    {
                        var isAccess = pillarIds.Count == 0 || pillarIds.Contains(x.Pillar.PillarID);

                        return new CrossCountryPillarValueDto
                        {
                            PillarID = x.Pillar.PillarID,
                            PillarName = x.Pillar.PillarName,
                            Value = isAccess ? x.Score?.AIProgress ?? 0 : 0,
                            IsAccess = isAccess,
                            DisplayOrder = x.Pillar.DisplayOrder
                        };
                    })
                    .OrderBy(x => !x.IsAccess)
                    .ThenBy(x => x.DisplayOrder)
                    .ToList();
                    var chartRow = new CrossCountryChartTableRowDto
                    {
                        CountryID = country.CountryID,
                        CountryName = country.CountryName,
                        PillarValues = pillarResults.ToList()
                    };
                    if (aiCountries?.TryGetValue(country.CountryID,out var aiCountryValue) ?? false)
                    {
                        chartRow.Value = aiCountryValue ?? 0;
                    }
                    response.TableData.Add(chartRow);

                    var series = new CrossCountryChartSeriesDto
                    {
                        Name = country.CountryName,
                        Data = pillarResults
                            .Where(x => x.IsAccess)
                            .Select(x => x.Value).ToList()
                    };
                    response.Series.Add(series);
                }

                return ResultResponseDto<AiCrossCountryResponseDto>.Success(response,new[] { "Pillars fetched successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetAICrossCountryPillars", ex);
                return ResultResponseDto<AiCrossCountryResponseDto>.Failure(new[] { "Error in getting pillar details" });
            }
        }

        public async Task<ResultResponseDto<bool>> ChangedAiCountryEvaluationStatus(ChangedAiCountryEvaluationStatusDto dto, int userID, UserRole userRole)
        {
            try
            {
                var v = _context.UserCountryMappings.Any(x => x.UserID == userID && x.CountryID == dto.CountryID);
                if ((v && userRole == UserRole.Analyst) || userRole == UserRole.Admin)
                {

                    var aiResponse = await _context.AICountryScores.Where(x => x.CountryID == dto.CountryID && x.Year == DateTime.UtcNow.Year).FirstOrDefaultAsync();
                    if (aiResponse != null)
                    {
                        aiResponse.IsVerified = dto.IsVerified;
                        aiResponse.VerifiedBy = userID;
                        
                        await _context.SaveChangesAsync();

                        _download.InsertAnalyticalLayerResults(dto.CountryID);
                        return ResultResponseDto<bool>.Success(true, new[] { dto.IsVerified ? "Finalize and lock the AI-generated score successfully" : "Reject the current AI-generated score Successfully" });
                    }
                }
                return ResultResponseDto<bool>.Failure(new[] { "Invalid country, please try again" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in ChangedAiCountryEvaluationStatus", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Error in Changed AiCountry Evaluation Status" });
            }
        }
        public async Task<ResultResponseDto<bool>> RegenerateAiSearch(RegenerateAiSearchDto dto,int userID, UserRole userRole)
        {
            try
            {
                if (dto.QuestionEnable)
                {
                    var currentYear = DateTime.Now.Year;

                    var aiQuestionList = await _context.AIEstimatedQuestionScores
                        .Where(x => x.CountryID == dto.CountryID && x.Year == currentYear)
                        .ToListAsync();

                    if (aiQuestionList.Count > 0)
                    {
                        _context.AIEstimatedQuestionScores.RemoveRange(aiQuestionList);
                        await _context.SaveChangesAsync();
                    }
                }


                await _download.AiResearchByCountryId(dto.CountryID, dto.CountryEnable, dto.PillarEnable, dto.QuestionEnable);
                var aiResponse = await _context.AICountryScores.FirstOrDefaultAsync(x => x.CountryID == dto.CountryID);
                if(aiResponse != null)
                {
                    aiResponse.IsVerified = false;
                }
                // Assign viewers (optional)

                var aIUserCountryMappingsList = await _context.AIUserCountryMappings.Where(x => x.CountryID == dto.CountryID).ToListAsync();

                var um = _context.UserCountryMappings.Where(x => !x.IsDeleted && x.CountryID == dto.CountryID && dto.ViewerUserIDs.Contains(x.UserID));
                var valid = um.All(x => dto.ViewerUserIDs.Contains(x.UserID));

                string msg = "Evaluator not have access of this county please try again";

                if (dto.ViewerUserIDs != null && dto.ViewerUserIDs.Any() && valid)
                {
                    var existingMappings = aIUserCountryMappingsList.Where(x => dto.ViewerUserIDs.Contains(x.UserID));


                    var existingUserIds = existingMappings.Select(x => x.UserID).ToHashSet();

                    // Update existing mappings
                    foreach (var mapping in existingMappings)
                    {
                        mapping.IsActive = true;
                        mapping.UpdatedAt = DateTime.UtcNow;
                        mapping.AssignBy = userID;
                        mapping.Comment = string.Empty;
                    }

                    // Insert new mappings
                    var newMappings = dto.ViewerUserIDs
                        .Where(userId => !existingUserIds.Contains(userId))
                        .Select(userId => new AIUserCountryMapping
                        {
                            UserID = userId,
                            CountryID = dto.CountryID,
                            AssignBy = userID,
                            UpdatedAt = DateTime.UtcNow,
                            IsActive = true
                        });

                    await _context.AIUserCountryMappings.AddRangeAsync(newMappings);
                    msg = "Evaluator have access to view the country";
                }
                else if(aIUserCountryMappingsList.Count > 0)
                {
                    foreach (var mapping in aIUserCountryMappingsList)
                    {
                        mapping.IsActive = false;
                        mapping.UpdatedAt = DateTime.UtcNow;
                        mapping.AssignBy = userID;
                        mapping.Comment = string.Empty;
                    }
                }

                var msglist = new List<string>
                {
                    "AI research import has been initiated successfully"
                };

                if (dto.ViewerUserIDs != null && dto.ViewerUserIDs.Any())
                {
                    msglist.Add(msg);
                }
                await _context.SaveChangesAsync();
                return ResultResponseDto<bool>.Success(true, msglist);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in RegenerateAiSearch", ex);

                return ResultResponseDto<bool>.Failure(new[] { "Something went wrong while importing AI research. Please try again later." });
            }
        }
        public async Task<ResultResponseDto<bool>> AddComment(AddCommentDto dto, int userID, UserRole userRole)
        {
            try
            {
                var aIUserCityMappings = await _context.AIUserCountryMappings.FirstOrDefaultAsync(x => x.UserID == userID && x.IsActive && x.CountryID == dto.CountryID);
                if (aIUserCityMappings !=null && userRole == UserRole.Evaluator)
                {
                    aIUserCityMappings.Comment = dto.Comment;

                    await _context.SaveChangesAsync();


                    await _context.SaveChangesAsync();
                    return ResultResponseDto<bool>.Success(true, new[] {"Comment Added Successfully"});

                }
                return ResultResponseDto<bool>.Failure(new[] { "Invalid country, please try again" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in ChangedAiCountryEvaluationStatus", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Error in Changed AiCountry Evaluation Status" });
            }
        }
        public async Task<ResultResponseDto<bool>> RegeneratePillarAiSearch(RegeneratePillarAiSearchDto channel, int userID, UserRole userRole)
        {
            try
            {
                if (channel.QuestionEnable)
                {
                    var currentYear = DateTime.Now.Year;
                    var aiQuestionList = await _context.AIEstimatedQuestionScores.Where(x => x.CountryID == channel.CountryID && x.PillarID== channel.PillarID && x.Year == currentYear).ToListAsync();
                    if (aiQuestionList.Count > 0)
                    {
                        _context.AIEstimatedQuestionScores.RemoveRange(aiQuestionList);
                        await _context.SaveChangesAsync();
                    }

                    await _iAIAnalayzeService.AnalyzeQuestionsOfCountryPillar(channel.CountryID, channel.PillarID);
                }

                if (channel.PillarEnable)
                    await _iAIAnalayzeService.AnalyzeSinglePillar(channel.CountryID,channel.PillarID);


                var msglist = new List<string>
                {
                    "AI research import has been initiated successfully"
                };
               
                await _context.SaveChangesAsync();
                return ResultResponseDto<bool>.Success(true, msglist);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in RegenerateAiSearch", ex);

                return ResultResponseDto<bool>.Failure(new[] { "Something went wrong while importing AI research. Please try again later." });
            }
        }

        public async Task<AiCountrySummeryDto> GetCountryAiSummeryDetail(int userID, UserRole userRole, int? CountryID, int year)
        {
            var query = await GetCountryAiSummeryDetails(userID, userRole, CountryID, year);
            var countryDetails = await query.FirstAsync();
            int pillarCount = _appSettings.PillarCount;
            if (userRole != UserRole.CountryUser)
            {
                var progress = await _commonService.GetCountriesProgressAsync(userID, (int)userRole, DateTime.Now.Year);

                var countries = progress.Where(x => x.CountryID == CountryID);

               if(countries != null)
                {
                    var countryScore = countries
                        .Select(x => x.ScoreProgress)
                        .DefaultIfEmpty(0)
                        .Sum();
                    countryScore = Math.Round(countryScore / pillarCount, 2);

                    countryDetails.EvaluatorScore = Math.Round(countryScore,2);
                    countryDetails.Discrepancy = Math.Abs(countryScore - (countryDetails.AIProgress ?? 0));
               }
            }
            return countryDetails;
        }
        private async Task<List<KpiChartItem>> GetAccessKpis(int userID, UserRole role, int? CountryID, int year = 0, bool isAiScore = true)
        {
            var startDate = new DateTime(year, 1, 1);
            var endDate = new DateTime(year + 1, 1, 1);

            var baseQuery = _context.AnalyticalLayerResults
                .AsNoTracking()
                .Include(ar => ar.AnalyticalLayer)
                    .ThenInclude(al => al.FiveLevelInterpretations)
                .Include(ar => ar.Country)
                .Where(x => x.AiLastUpdated >= startDate && x.AiLastUpdated < endDate);

            if (role == UserRole.CountryUser)
            {
                var validCountries = _context.PublicUserCountryMappings
                    .Where(x => x.IsActive && x.UserID == userID)
                    .Select(x => x.CountryID);

                var validPillarIds = _context.CountryUserPillarMappings
                    .Where(x => x.IsActive && x.UserID == userID)
                    .Select(x => x.PillarID);

                var validLayerIds = _context.AnalyticalLayerPillarMappings
                    .Where(x => validPillarIds.Contains(x.PillarID))
                    .Select(x => x.LayerID)
                    .Distinct();

                baseQuery = baseQuery
                    .Where(ar =>
                        validCountries.Contains(ar.CountryID) &&
                        validLayerIds.Contains(ar.LayerID));
            }

            var kpiRaw = baseQuery
            .Where(x => !CountryID.HasValue || x.CountryID == CountryID)
            .Select(x => new
            {
                KpiShortName = x.AnalyticalLayer.LayerCode,
                KpiName = x.AnalyticalLayer.LayerName,
                CountryID = x.CountryID,
                AiCalValue5 = x.AiCalValue5,
                CalValue5 = x.CalValue5,
                Definition = StripHtml(x.AnalyticalLayer.Purpose),
                AnalyticalLayer = x.AnalyticalLayer
            })
            .Select(x => new
            {
                x.KpiShortName,
                x.KpiName,
                x.CountryID,
                x.AiCalValue5,
                x.CalValue5,
                LayerID = x.AnalyticalLayer.LayerID,
                Definition = x.Definition,
                Interpretation = x.AnalyticalLayer.FiveLevelInterpretations.Select(i => new FiveLevelInterpretationsDto
                (
                   i.InterpretationID,
                   i.LayerID,
                   i.MinRange,
                   i.MaxRange,
                   i.Condition,
                   i.StrategicAction
                )).ToList()

            }).OrderBy(x => x.LayerID);

            var kpis = await kpiRaw
                .Select(k => new KpiChartItem(k.KpiShortName, k.KpiName, isAiScore && role == UserRole.Admin ? k.AiCalValue5 : k.CalValue5, k.Definition, k.CountryID, k.Interpretation))
                .ToListAsync();

            return kpis ?? new List<KpiChartItem>();
        }
        public async Task<List<AiCountrySummeryDto>> GetAllCountryAiSummeryDetail(int userID, UserRole userRole, int year)
        {
            var query = await GetCountryAiSummeryDetails(userID, userRole, null, year);
            var countriesDetails = await query.ToListAsync();
            int pillarCount = _appSettings.PillarCount;
            if (userRole != UserRole.CountryUser)
            {
                foreach (var countryDetails in countriesDetails)
                {
                    var progress = await _commonService.GetCountriesProgressAsync(userID, (int)userRole, DateTime.Now.Year);

                    var countries = progress.Where(x => x.CountryID == countryDetails.CountryID);

                    if (countries != null)
                    {
                        var countryScore = countries
                            .Select(x => x.ScoreProgress)
                            .DefaultIfEmpty(0)
                            .Sum();
                        countryScore = Math.Round(countryScore / pillarCount, 2);

                        countryDetails.EvaluatorScore = Math.Round(countryScore, 2);
                        countryDetails.Discrepancy = Math.Abs(countryScore - (countryDetails.AIProgress ?? 0));
                    }
                }

            }
            return countriesDetails;
        }        

        public async Task<byte[]> GenerateAllCountryDetailsReport(List<AiCountrySummeryDto> countriesDetails, UserRole userRole, int userID, int year, IServices.DocumentFormat format = IServices.DocumentFormat.Pdf)
        {
            try
            {
                var pillars = await GetAllCountriesAIPillars(userID, userRole, year);

                var kpis = new List<KpiChartItem>();

                var recordAvailable = pillars.Result.Any(x => countriesDetails.Select(x => x.CountryID).Contains(x.Key));
                if (recordAvailable)
                {
                    var document = await _documentGeneratorService.GenerateAllCountriesDetails(countriesDetails, pillars.Result, kpis, userRole, format);

                    return document;
                }
                else
                {
                    return Array.Empty<byte>();
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GeneratecountryDetailsReport", ex);
                return Array.Empty<byte>();
            }
        }
        public async Task<ResultResponseDto<Dictionary<int, List<AiCountryPillarResponse>>>> GetAllCountriesAIPillars(
         int userID, UserRole userRole, int currentYear = 0)
        {
            try
            {
                int pillarCount = _appSettings.PillarCount;
                currentYear = currentYear == 0 ? DateTime.Now.Year : currentYear;
                var firstDate = new DateTime(currentYear, 1, 1);

                var scores = await _context.AIPillarScores
                    .Where(x => x.UpdatedAt >= firstDate && x.Year == currentYear)
                    .Include(x => x.Country)
                    .Include(x => x.DataSourceCitations)
                    .ToListAsync();

                List<int> pillarIds = new();
                if (userRole == UserRole.CountryUser)
                {
                    pillarIds = await _context.CountryUserPillarMappings
                        .Where(x => x.IsActive && x.UserID == userID)
                        .Select(x => x.PillarID)
                        .Distinct()
                        .ToListAsync();
                }

                var pillars = await _context.Pillars.Select(x => new
                {
                    x.PillarID,
                    x.PillarName,
                    x.DisplayOrder,
                    x.ImagePath,
                    TotalQuestions = x.Questions.Count()
                }).ToListAsync();

                var CountryIDs = scores.Select(x => x.CountryID).Distinct().ToList();

                var result = new Dictionary<int, List<AiCountryPillarResponse>>();

                foreach (var CountryID in CountryIDs)
                {
                    var countryScores = scores.Where(x => x.CountryID == CountryID).ToList();

                    var pillarResults = pillars
                        .GroupJoin(
                            countryScores,
                            p => p.PillarID,
                            s => s.PillarID,
                            (pillar, score) => new { pillar, score = score.FirstOrDefault() }
                        )
                        .Select(x =>
                        {
                            var isAccess = pillarIds.Count == 0 || pillarIds.Contains(x.pillar.PillarID);

                            var r = new AiCountryPillarResponse
                            {
                                PillarScoreID = x.score?.PillarScoreID ?? 0,
                                CountryID = x.score?.CountryID ?? CountryID,
                                CountryName = x.score?.Country?.CountryName ?? "",
                                Continent = x.score?.Country?.Continent ?? "",                                
                                PillarID = x.pillar.PillarID,
                                PillarName = x.pillar.PillarName,
                                DisplayOrder = x.pillar.DisplayOrder,
                                ImagePath = x.pillar.ImagePath,
                                IsAccess = isAccess
                            };

                            if (isAccess && x.score != null)
                            {
                                r.AIDataYear = x.score.Year;
                                r.AIScore = x.score.AIScore;
                                r.AIProgress = x.score.AIProgress;
                                r.EvaluatorScore = x.score.EvaluatorScore;
                                r.Discrepancy = x.score.Discrepancy;
                                r.ConfidenceLevel = x.score.ConfidenceLevel;
                                r.EvidenceSummary = x.score.EvidenceSummary;
                                r.StructuralEvidence = x.score.StructuralEvidence;
                                r.OperationalEvidence = x.score.OperationalEvidence;
                                r.OutcomeEvidence = x.score.OutcomeEvidence;
                                r.PerceptionEvidence = x.score.PerceptionEvidence;
                                r.TemporalScope = x.score.TemporalScope;
                                r.DistortionScreening = x.score.DistortionScreening;
                                r.RelationalIntegrity = x.score.RelationalIntegrity;
                                r.StressPoliticalShock = x.score.StressPoliticalShock;
                                r.StressEconomicShock = x.score.StressEconomicShock;
                                r.StressNarrativeShock = x.score.StressNarrativeShock;
                                r.StressOverallResilience = x.score.StressOverallResilience;
                                r.StressScoreAdjustment = x.score.StressScoreAdjustment;
                                r.InequalityAdjustment = x.score.InequalityAdjustment;
                                r.OpacityRisk = x.score.OpacityRisk;
                                r.NonCompensationNote = x.score.NonCompensationNote;
                                r.GeographicEquityNote = x.score.GeographicEquityNote;
                                r.InstitutionalAssessment = x.score.InstitutionalAssessment;
                                r.DataGapAnalysis = x.score.DataGapAnalysis;
                                r.RedFlag = x.score.RedFlag;
                                r.DataSourceCitations = x.score.DataSourceCitations;
                                r.UpdatedAt = x.score.UpdatedAt;
                            }

                            return r;
                        })
                        .OrderBy(x => !x.IsAccess)
                        .ThenBy(x => x.DisplayOrder)
                        .ToList();

                    result.Add(CountryID, pillarResults);
                }

                var progress = await _commonService.GetCountriesProgressAsync(userID, (int)userRole, currentYear);

                var answeredQuestions = await _context.AIEstimatedQuestionScores
                    .Where(x => x.Year == currentYear)
                    .GroupBy(x => new { x.CountryID, x.PillarID })
                    .Select(g => new
                    {
                        g.Key.CountryID,
                        g.Key.PillarID,
                        AnsweredQuestions = g.Count()
                    })
                    .ToListAsync();

                foreach (var country in result)
                {
                    foreach (var c in country.Value)
                    {
                        var totalQuestions = pillars.FirstOrDefault(x => x.PillarID == c.PillarID)?.TotalQuestions ?? 1;

                        var answeredQuestion = answeredQuestions
                            .FirstOrDefault(x => x.CountryID == country.Key && x.PillarID == c.PillarID)?.AnsweredQuestions ?? 0;

                        var countryScore = progress
                            .Where(x => x.CountryID == country.Key && x.PillarID == c.PillarID)
                            .Select(x => x.ScoreProgress)
                            .DefaultIfEmpty(0)
                            .Sum();

                        countryScore = Math.Round(countryScore / pillarCount, 2);

                        c.EvaluatorScore = countryScore;
                        c.Discrepancy = Math.Abs(countryScore - (c.AIProgress ?? 0));
                        c.AICompletionRate = answeredQuestion * 100.0M / totalQuestions;
                    }
                }

                var response = ResultResponseDto<Dictionary<int, List<AiCountryPillarResponse>>>
                    .Success(result, new[] { "All countries pillars fetched successfully" });

                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in getAllCountriesAIPillars", ex);

                return ResultResponseDto<Dictionary<int, List<AiCountryPillarResponse>>>
                    .Failure(new[] { "Error in getting countries pillar details" });
            }
        }
        public record KpiChartItem(string ShortName, string Name, decimal? Value, string? Definition, int? CountryID, List<FiveLevelInterpretationsDto> InterPretation);

        public record FiveLevelInterpretationsDto(
        int InterpretationID,
        int LayerID,
        decimal? MinRange,
        decimal? MaxRange,
        string Condition,
        string StrategicAction
   );
        public record PillarChartItem(string ShortName, string Name, decimal? Value);
        #region TransferAssessment
        public async Task<ResultResponseDto<string>> AITransferAssessment(AITransferAssessmentRequestDto r, int userID, UserRole userRole)
        {
            try
            {
                var currentDate = DateTime.Now;
                var year = currentDate.Year;

                if (userRole == UserRole.CountryUser || userRole == UserRole.Evaluator)
                {
                    return ResultResponseDto<string>.Failure(new[] { "Failed to transfer assessment, You don't have access." });
                }

                if (userRole == UserRole.Analyst)
                {
                    r.TransferToUserID = userID;

                    var validCity = _context.UserCountryMappings.Any(x => !x.IsDeleted && x.CountryID == r.CountryID && x.UserID == userID);

                    if (!validCity)
                    {
                        return ResultResponseDto<string>.Failure(new[] { "This assessment can’t be imported because the selected user hasn’t been assigned to this country yet." });
                    }
                }

                var aiAssessmentData = await _context.AIEstimatedQuestionScores
                                    .Where(x => x.CountryID == r.CountryID && x.Year == year)
                                    .ToListAsync();

                var aiAssessmentQuestions = aiAssessmentData
                    .GroupBy(x => x.PillarID)
                    .ToDictionary(g => g.Key, g => g.ToList());

                if (aiAssessmentQuestions == null || aiAssessmentQuestions.Count==0)
                    return ResultResponseDto<string>.Failure(new[] { "There is no ai assessment is available for this country" });


                var userCountryMapping = await _context.UserCountryMappings.FirstOrDefaultAsync(x => !x.IsDeleted && x.CountryID == r.CountryID && x.UserID == r.TransferToUserID);

                if (userCountryMapping == null)
                    return ResultResponseDto<string>.Failure(new[] { "This assessment can’t be imported because the selected user hasn’t been assigned to this country yet." });


                // Load existing assessment for that user/country/year (with pillars/responses)
                var existingAssessment = await _context.Assessments
                    .Include(a => a.PillarAssessments)
                        .ThenInclude(p => p.Responses)
                    .FirstOrDefaultAsync(a => a.UserCountryMappingID == userCountryMapping.UserCountryMappingID &&
                                              a.UpdatedAt.Year == year);

                if (existingAssessment == null)
                {
                    existingAssessment = new Assessment
                    {
                        UserCountryMappingID = userCountryMapping.UserCountryMappingID,
                        CreatedAt = currentDate,
                        UpdatedAt = currentDate,
                        IsActive = true,
                        AssessmentPhase = userRole == UserRole.Admin ? AssessmentPhase.Completed : AssessmentPhase.InProgress,
                        PillarAssessments = new List<PillarAssessment>()
                    };

                    _context.Assessments.Add(existingAssessment);
                }
                else
                {
                    existingAssessment.UpdatedAt = currentDate;
                    existingAssessment.AssessmentPhase =  AssessmentPhase.InProgress;
                }

                var questions = await _context.Questions.Include(x => x.QuestionOptions).ToDictionaryAsync(q => q.QuestionID, q => q);

                // Transfer pillar data
                foreach (var pillar in aiAssessmentQuestions)
                {
                    var existingPillar = existingAssessment.PillarAssessments
                        .FirstOrDefault(x => x.PillarID == pillar.Key);

                    if (existingPillar == null)
                    {
                        existingPillar = new PillarAssessment
                        {
                            PillarID = pillar.Key,
                            Responses = new List<AssessmentResponse>()
                        };
                        existingAssessment.PillarAssessments.Add(existingPillar);
                    }

                    // Add/Update responses
                    foreach (var response in pillar.Value)
                    {
                        var existingResponse = existingPillar.Responses
                            .FirstOrDefault(rp => rp.QuestionID == response.QuestionID);

                        var qustion = questions.ContainsKey(response.QuestionID) ? questions[response.QuestionID] : null;
                        if (qustion == null)
                            continue;

                        int? score = response.AIScore != null ? (int?)Math.Round(response.AIScore.Value, 0) : null;

                        var option = qustion.QuestionOptions.FirstOrDefault(x => x.ScoreValue == score);
                        if (option == null)
                            continue;

                        if (existingResponse == null)
                        {

                            existingPillar.Responses.Add(new AssessmentResponse
                            {
                                QuestionID = response.QuestionID,
                                QuestionOptionID = option.OptionID,
                                Justification = response.EvidenceSummary,
                                Source = response.SourceDataExtract + "SourceURL : " + response.SourceURL,
                                Score = (ScoreValue?)score
                            });
                        }
                        else
                        {
                            existingResponse.QuestionOptionID = option.OptionID;
                            existingResponse.Justification = response.EvidenceSummary;
                            existingResponse.Score = (ScoreValue?)score;
                            existingResponse.Source = response.SourceDataExtract + " SourceURL : " + response.SourceURL;
                        }
                    }

                    // Delete responses not present in transferAssessment
                    var transferQuestionIds = pillar.Value.Select(x => x.QuestionID).ToHashSet();
                    var toDeleteResponses = existingPillar.Responses
                        .Where(x => !transferQuestionIds.Contains(x.QuestionID))
                        .ToList();

                    foreach (var resp in toDeleteResponses)
                    {
                        //existingPillar.Responses.Remove(resp);
                        _context.AssessmentResponses.Remove(resp);
                    }
                }

                // Delete pillars not present in transferAssessment
                var transferPillarIds = aiAssessmentQuestions.Select(x => x.Key).ToHashSet();
                var toDeletePillars = existingAssessment.PillarAssessments
                    .Where(x => !transferPillarIds.Contains(x.PillarID))
                    .ToList();

                foreach (var pillar in toDeletePillars)
                {
                    //existingAssessment.PillarAssessments.Remove(pillar);
                    _context.PillarAssessments.Remove(pillar);
                }
                if (existingAssessment.AssessmentPhase == AssessmentPhase.Completed)
                {
                    _download.InsertAnalyticalLayerResults(r.CountryID);
                }                
                await _context.SaveChangesAsync();

                return ResultResponseDto<string>.Success("", new[] { "Assessment transferred successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in TransferAssessment", ex);
                return ResultResponseDto<string>.Failure(new[] { "Failed to transfer assessment, please try again later." });
            }
        }
        #endregion TransferAssessment
        public async Task<ResultResponseDto<string>> ReCalculateKpis(int userID, UserRole userRole)
        {
            try
            {
                if (userRole != UserRole.Admin)
                {
                    return ResultResponseDto<string>.Failure(new[] { "Failed to recalculate KPIs, You don't have access." });
                }

                await _context.Database.ExecuteSqlRawAsync("EXEC sp_AiRecalculateCountryScore");

                await _context.Database.ExecuteSqlRawAsync("EXEC sp_InsertAnalyticalLayerResults");

                await _context.Database.ExecuteSqlRawAsync("EXEC sp_AiInsertAnalyticalLayerResults");

                return ResultResponseDto<string>.Success("", new[] { "KPI recalculation has been initiated successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in ReCalculateKpis", ex);
                return ResultResponseDto<string>.Failure(new[] { "Failed to recalculate KPIs, please try again later." });
            }
        }

        public static string StripHtml(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Remove HTML tags
            var noTags = Regex.Replace(input, "<.*?>", string.Empty);

            // Decode HTML entities (e.g., &mdash;)
            return WebUtility.HtmlDecode(noTags);
        }


        #endregion
    }
}
