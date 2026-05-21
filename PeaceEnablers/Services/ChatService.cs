
using Microsoft.Extensions.Caching.Memory;
using PeaceEnablers.Common.Interface;
using PeaceEnablers.Common.Models;
using PeaceEnablers.Data;
using PeaceEnablers.Dtos.chatDto;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;

namespace PeaceEnablers.Services
{
    public class ChatService : IChatService
    {
        #region  constructor
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly IAIAnalyzeService _aIAnalyzeService;
        private readonly IMemoryCache _cache;
        private readonly ICommonService _commonService;
        public ChatService(ApplicationDbContext context, IMemoryCache cache,
            IAppLogger appLogger, IAIAnalyzeService aIAnalyzeService, ICommonService commonService)
        {
            _context = context;
            _appLogger = appLogger;
            _aIAnalyzeService = aIAnalyzeService;
            _cache = cache;
            _commonService = commonService;
        }
        public async Task<ResultResponseDto<List<AIAssistantFAQDto>>> GetAssistantFAQDs(int userId, UserRole userRole)
        {
            try
            {
                var faqs = _context.AIAssistantFAQ
                    .Where(x => x.IsActive)
                    .Select(x => new AIAssistantFAQDto
                    {
                        FAQID = x.FAQID,
                        Related = x.Related,
                        Category = x.Category,
                        QuestionText = x.QuestionText,
                        DisplayOrder = x.DisplayOrder,
                        IsAnsweredFaq =  !string.IsNullOrEmpty(x.AnswerText)
                    }).ToList();

                return ResultResponseDto<List<AIAssistantFAQDto>>.Success(faqs, new[] { "Faqs get successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("An error occurred while getting the GetAssistantFAQDs request.", ex);
                return ResultResponseDto<List<AIAssistantFAQDto>>.Failure(new[] { "An error occurred while processing your request. Please try again later." });
            }
        }

        public async Task<ResultResponseDto<ChatResponseDto>> AskAboutCountry(CountryChatRequestDto request, int userId, UserRole userRole)
        {
            try
            {
                if(userRole == UserRole.CountryUser)
                {
                    var isValidCountry = _context.PublicUserCountryMappings.Where(x => x.UserID == userId).Any(c => c.CountryID == request.CountryID);
                    if (!isValidCountry)
                    {
                        return ResultResponseDto<ChatResponseDto>.Failure(new[] { "You don't have access to this country data." });
                    }
                }

                var r = new ChatCountryAskQuestionRequest
                {
                    CountryID = request.CountryID,
                    PillarID = request.PillarID,
                    QuestionText = request.QuestionText,
                    FAQID = request.FAQID,
                    HistoryText = request.HistoryText
                };

                var resutl = await _aIAnalyzeService.ChatCountryAsk(r);
          
                if (resutl == null || resutl.Success != true)
                {
                    return ResultResponseDto<ChatResponseDto>.Failure(
                        new[] { resutl?.Message ?? "Failed to query request from PEM Aevum." }
                    );
                }

                return ResultResponseDto<ChatResponseDto>.Success(new ChatResponseDto
                {
                    CountryID = request.CountryID,
                    PillarID = request.PillarID,
                    QuestionText = request.QuestionText,
                    FAQID = request.FAQID,
                    ResponseText = resutl.Result ?? "No response from ."
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("An error occurred while processing the AskAboutCountry request.", ex);
                return ResultResponseDto<ChatResponseDto>.Failure(new[] { "An error occurred while processing your request. Please try again later." });
            }
        }

        public async Task<ResultResponseDto<ChatResponseDto>> AskAboutGlobal(ChatGlobalAskQuestionRequestDto request, int userId, UserRole userRole)
        {
            try
            {
                var r = new ChatGlobalAskQuestionRequest
                {  
                    QuestionText = request.QuestionText,
                    FAQID = request.FAQID,
                    HistoryText = request.HistoryText
                };

                var resutl = await _aIAnalyzeService.ChatGlobalAsk(r);

                if (resutl == null || resutl.Success != true)
                {
                    return ResultResponseDto<ChatResponseDto>.Failure(
                        new[] { resutl?.Message ?? "Failed to query request from PEM Aevum." }
                    );
                }

                return ResultResponseDto<ChatResponseDto>.Success(new ChatResponseDto
                {       
                    QuestionText = request.QuestionText,
                    FAQID = request.FAQID,
                    ResponseText = resutl.Result ?? "An error occurred or we do not have an answer for that."
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("An error occurred while processing the AskAboutGlobal request.", ex);
                return ResultResponseDto<ChatResponseDto>.Failure(new[] { "An error occurred while processing your request. Please try again later." });
            }
        }

        public async Task<ResultResponseDto<ChatResponseDto>> CrossComparision(CrossComparisionRequestDto request, int userId, UserRole userRole)
        {
            try
            {
                if (userRole == UserRole.CountryUser)
                {
                    var userCountryIds = _context.PublicUserCountryMappings
                        .Where(x=>x.UserID == userId)
                        .Select(x => x.CountryID)
                        .ToList();

                    var isValidCountry = request.CountryIDs
                        .All(id => userCountryIds.Contains(id));

                    if (!isValidCountry)
                    {
                        return ResultResponseDto<ChatResponseDto>
                            .Failure(new[] { "You don't have access to this country data." });
                    }
                }
                var r = new CrossComparisionRequest
                {  
                    CountryIDs = request.CountryIDs,
                    QuestionText = request.QuestionText,
                    HistoryText = request.HistoryText
                };

                var resutl = await _aIAnalyzeService.CrossComparision(r);

                if (resutl == null || resutl.Success != true)
                {
                    return ResultResponseDto<ChatResponseDto>.Failure(
                        new[] { resutl?.Message ?? "Failed to query request from PEM Aevum." }
                    );
                }

                return ResultResponseDto<ChatResponseDto>.Success(new ChatResponseDto
                {       
                    QuestionText = request.QuestionText,
                    FAQID = null,
                    ResponseText = resutl.Result ?? "An error occurred or we do not have an answer for that."
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("An error occurred while processing the AskAboutGlobal request.", ex);
                return ResultResponseDto<ChatResponseDto>.Failure(new[] { "An error occurred while processing your request. Please try again later." });
            }
        }
        public async Task<ResultResponseDto<ChatCountryExecutiveSlidesResponse>> GetCountrySlides(int CountryId, int userId, UserRole userRole)
        {
            string cacheKey = $"CountrySlides_{CountryId}";

            try
            {
                if (userRole == UserRole.CountryUser)
                {
                    var isValidCountry = _context.PublicUserCountryMappings.Where(x => x.UserID == userId).Any(c => c.CountryID == CountryId);
                    if (!isValidCountry)
                    {
                        return ResultResponseDto<ChatCountryExecutiveSlidesResponse>.Failure(new[] { "You don't have access to this country data." });
                    }
                }

                var year = DateTime.UtcNow.Year;

                var countryExists = await _commonService.GetCountriesRankings(CountryId, year);

                var country = countryExists.FirstOrDefault(x=>x.CountryID == CountryId);

                if (country == null)
                {
                   return ResultResponseDto<ChatCountryExecutiveSlidesResponse>.Failure(new[] { "Country not found." });
                }                

                var pillars = (
                    from p in _context.Pillars

                    join x in _context.AIPillarScores
                        .Where(a => a.CountryID == country.CountryID
                                 && a.Year == country.DataYear)
                    on p.PillarID equals x.PillarID into pillarScores

                    from score in pillarScores.DefaultIfEmpty()

                    select new PillarsUserHistroyResponseDto
                    {
                        PillarID = p.PillarID,
                        PillarName = p.PillarName ?? "",
                        DisplayOrder = p.DisplayOrder,
                        PillarScore = score != null ? score.AIProgress ?? 0 : 0,
                        ImagePath = p.ImagePath
                    }
                ).ToList();

                if (userRole == UserRole.CountryUser)
                {
                    var validPillars = _context.CountryUserPillarMappings.Where(x => x.UserID == userId).Select(x => x.PillarID);
                    pillars = pillars.Where(x => validPillars.Contains(x.PillarID)).ToList();
                }
                


                var countryResult = new CountryRankingResponseDto
                {
                    Continent = country.Continent,
                    CountryID = country.CountryID,
                    CountryName = country.CountryName,
                    CountryRank = country.CountryRank,
                    CountryAIScore = country.CountryAIScore,
                    DataYear = country.DataYear,
                    Region = country.Region,
                    RegionRank= country.RegionRank,
                    TotalCountry = country.TotalCountry,
                    TotalCountryInRegion = country.TotalCountryInRegion,
                    Pillars = pillars.OrderBy(p => p.DisplayOrder).ToList()
                };

                if (_cache.TryGetValue(cacheKey, out ChatCountryExecutiveSlidesResponse cachedResult))
                {
                    cachedResult.Result.Country = countryResult;

                    return ResultResponseDto<ChatCountryExecutiveSlidesResponse>.Success(
                        cachedResult,
                        new List<string>
                        {
                            "Country executive slides fetched successfully from cache."
                        }
                    );
                }

                // ✅ Fetch from AI service
                var result = await _aIAnalyzeService.GetCountrySlides(CountryId);

                if (result == null || result.Success != true)
                {
                    return ResultResponseDto<ChatCountryExecutiveSlidesResponse>.Failure(
                        new[]
                        {
                            result?.Message ??
                            "Failed to fetch Country executive slides from PEM Aevum."
                        }
                    );
                }

                // ✅ Store in cache
                _cache.Set(cacheKey,  result,
                    new MemoryCacheEntryOptions
                    { 
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12),
                        SlidingExpiration = TimeSpan.FromHours(10),
                        Priority = CacheItemPriority.High
                    });

                result.Result.Country = countryResult;
                return ResultResponseDto<ChatCountryExecutiveSlidesResponse>.Success(
                    result,
                    new List<string>
                    {
                         "Country executive slides fetched successfully."
                    }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync(
                    "An error occurred while processing the GetCountrySlides request.",
                    ex
                );

                return ResultResponseDto<ChatCountryExecutiveSlidesResponse>.Failure(
                    new[]
                    {
                        "An error occurred while processing your request. Please try again later."
                    }
                );
            }
        }


        #endregion
    }
}
