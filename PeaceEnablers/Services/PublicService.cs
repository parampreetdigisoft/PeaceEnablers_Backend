using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PeaceEnablers.Common.Implementation;
using PeaceEnablers.Common.Models;
using PeaceEnablers.Data;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Dtos.PublicDto;
using PeaceEnablers.IServices;
using System.Text.Json;

namespace PeaceEnablers.Services
{
    [AllowAnonymous]
    public class PublicService : IPublicService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;
        public PublicService(ApplicationDbContext context, IAppLogger appLogger, IWebHostEnvironment env, IMemoryCache cache)
        {
            _context = context;
            _appLogger = appLogger;
            _env = env;
            _cache = cache;
        }
        public async Task<ResultResponseDto<List<PartnerCountryResponseDto>>> getAllCountries()
        {
            try
            {
                var result = await _context.Countries.Where(c => c.IsActive && !c.IsDeleted).
                 Select(c => new PartnerCountryResponseDto
                 {
                     CountryID = c.CountryID,                     
                     CountryName = c.CountryName,
                     CountryCode = c.CountryCode,
                     Continent = c.Continent,
                     
                 }).OrderBy(x => x.CountryName).ToListAsync();

                return ResultResponseDto<List<PartnerCountryResponseDto>>.Success(result, new string[] { "get All Countries successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in getAllCountries", ex);
                return ResultResponseDto<List<PartnerCountryResponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<PartnerCountryFilterResponse>> GetPartnerCountriesFilterRecord()
        {
            try
            {
                // Fetch all active Countries once
                var activeCountries = await _context.Countries
                    .Where(x => !x.IsDeleted)
                    .ToListAsync();

                var res = new PartnerCountryFilterResponse
                {
                    Countries = activeCountries.Select(x=>x.CountryName)
                        .Distinct()
                        .ToList(),

                    //Countries = activeCountries
                    //    .Select(x => new PartnerCountryDto
                    //    {
                    //        CountryID = x.CountryID,
                    //        CountryName = x.CountryName
                    //    })
                    //    .ToList(),

                    Regions = activeCountries
                        .Select(x => x.Region)
                        .Where(r => !string.IsNullOrEmpty(r))
                        .Distinct()
                        .ToList()
                };

                return ResultResponseDto<PartnerCountryFilterResponse>.Success(
                    res,
                    new List<string> { "Get Countries history successfully" }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetPartnerCountriesFilterRecord", ex);
                return ResultResponseDto<PartnerCountryFilterResponse>.Failure(
                    new string[] { "Failed to get Partner City filter data" }
                );
            }
        }

        public async Task<ResultResponseDto<List<PillarResponseDto>>> GetAllPillarAsync()
        {
            try
            {
                var res =  await _context.Pillars
                .OrderBy(p => p.DisplayOrder)
                .Select(x => new PillarResponseDto
                {
                    DisplayOrder = x.DisplayOrder,
                    PillarID = x.PillarID,
                    PillarName = x.PillarName,
                    ImagePath = x.ImagePath
                }).ToListAsync();
                return ResultResponseDto<List<PillarResponseDto>>.Success(res, new List<string> { "Get Countries history successfully" });

            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetAllPillarAsync", ex);
                return ResultResponseDto<List<PillarResponseDto>>.Failure(new string[] { "Failed to get Piilar detail" });
            }
        }
        public async Task<PaginationResponse<PartnerCountryResponseDto>> GetPartnerCountries(PartnerCountryRequestDto request)
        {
            try
            {
                var year = DateTime.Now.Year;


                var cityQuery =
                   from c in _context.Countries.Where(x => !request.CountryID.HasValue || x.CountryID == request.CountryID)
                   join uc in _context.UserCountryMappings on c.CountryID equals uc.CountryID into ucg
                   from uc in ucg.DefaultIfEmpty()
                   join a in _context.Assessments on uc.UserCountryMappingID equals a.UserCountryMappingID into ag
                   from a in ag.DefaultIfEmpty()
                   join pa in _context.PillarAssessments.Where(x=> !request.PillarID.HasValue || x.PillarID == request.PillarID) 
                   on a.AssessmentID equals pa.AssessmentID into pag
                   from pa in pag.DefaultIfEmpty()
                   join r in _context.AssessmentResponses on pa.PillarAssessmentID equals r.PillarAssessmentID into rg
                   from r in rg.DefaultIfEmpty()
                   where !c.IsDeleted && 
                    (uc == null || !uc.IsDeleted) &&
                    (a == null || a.UpdatedAt.Year == year) 
                   group r by new
                   {
                       c.CountryID,                       
                       c.CountryCode,
                       c.Image,
                       c.Continent,
                       c.CountryName,
                       c.Region,
                       EvaluatorCount = _context.UserCountryMappings
                                           .Count(x => x.CountryID == c.CountryID && !x.IsDeleted)
                   }
                   into g
                   select new PartnerCountryResponseDto
                   {
                       CountryID = g.Key.CountryID,
                       Continent = g.Key.Continent,
                       CountryName = g.Key.CountryName,
                       CountryCode = g.Key.CountryCode,
                       Region = g.Key.Region,                       
                       Image = g.Key.Image,
                       Score = (decimal)g.Sum(x => (int?)x.Score ?? 0) / (g.Key.EvaluatorCount == 0 ? 1 : g.Key.EvaluatorCount),
                       HighScore = g.Max(x=>(int?)x.Score ?? 0),
                       LowerScore = g.Min(x => (int?)x.Score ?? 0),
                       Progress = ((decimal)g.Sum(x => (int?)x.Score ?? 0) * 100) / ((g.Key.EvaluatorCount == 0 ? 1 : g.Key.EvaluatorCount) * 4 * g.Count()),
                   };

                if (!string.IsNullOrWhiteSpace(request.Country))
                {
                    cityQuery = cityQuery.Where(c => c.CountryName.Contains(request.Country));
                }

                // Only filter by Region if a value is provided
                if (!string.IsNullOrWhiteSpace(request.Region))
                {
                    cityQuery = cityQuery.Where(c => c.Region != null && c.Region.Contains(request.Region));
                }

                var response = await cityQuery.ApplyPaginationAsync(request);

                return response;

            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetCountriesProgressByUserId", ex);
                return new();
            }
        }

        public async Task<CountryCityResponse> GetCountriesAndCountries_WithStaleSupport()
        {
            try
            {
                string jsonFilePath = Path.Combine(_env.WebRootPath, "data\\countries_cache.json");
                if (!File.Exists(jsonFilePath))
                    return new CountryCityResponse(); // ✅ NEVER return null

                var json = await File.ReadAllTextAsync(jsonFilePath);

                var data = JsonSerializer.Deserialize<CountryCityResponse>(json);

                return data ?? new CountryCityResponse();
            }
            catch (Exception ex)
            {
                // ✅ Optional: log error
                // _logger.LogError(ex, "Failed to load country-city file");

                return new CountryCityResponse(); // ✅ Safe fallback
            }
        }

        public async Task<ResultResponseDto<List<PromotedPillarsResponseDto>>> GetPromotedCountries()
        {
            const string cacheKey = "GetPromotedCountries";

            try
            {
                // ✅ Try get from cache
                if (_cache.TryGetValue(cacheKey, out List<PromotedPillarsResponseDto> cachedData))
                {
                    return ResultResponseDto<List<PromotedPillarsResponseDto>>.Success(
                        cachedData,
                        new List<string> { "Promoted cities fetched successfully" }
                    );
                }

                int currentYear = DateTime.Now.Year;

                var result = await _context.AIPillarScores
                    .Include(x => x.Country)
                    .Include(x => x.Pillar)
                    .Where(x =>
                        x.Year == currentYear &&
                        x.Country.IsActive &&
                        !x.Country.IsDeleted)
                    .GroupBy(x => new
                    {
                        x.PillarID,
                        x.Pillar.PillarName,
                        x.Pillar.DisplayOrder,
                        x.Pillar.ImagePath
                    })
                    .Select(g => new PromotedPillarsResponseDto
                    {
                        PillarID = g.Key.PillarID,
                        PillarName = g.Key.PillarName,
                        DisplayOrder = g.Key.DisplayOrder,
                        ImagePath = g.Key.ImagePath,
                        Countries = g
                            .OrderByDescending(x => x.AIProgress)
                            .Take(3)
                            .Select(c => new PromotedCountryResponseDto
                            {
                                CountryID = c.CountryID,
                                CountryName = c.Country.CountryName,
                                CountryCode = c.Country.CountryCode,
                                Continent = c.Country.Continent,
                                Region = c.Country.Region,
                                Image = c.Country.Image,
                                ScoreProgress = c.AIProgress,
                                Description = c.EvidenceSummary,
                            }).ToList()
                    }).OrderBy(p => p.DisplayOrder).ToListAsync();

                _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                    SlidingExpiration = TimeSpan.FromMinutes(2),
                    Priority = CacheItemPriority.High
                });


                return ResultResponseDto<List<PromotedPillarsResponseDto>>.Success(
                    result,
                    new List<string> { "Promoted Countries fetched successfully" }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in GetPromotedCountries", ex);
                return ResultResponseDto<List<PromotedPillarsResponseDto>>.Failure(
                    new[] { "Failed to get promoted Countries" }
                );
            }
        }

        public async Task<ResultResponseDto<List<PillarDmiResultDto>>> GetPillarsDmi()
        {
            const string cacheKey = "GetPillarsDmi";

            try
            {
                // ✅ Try get from cache
                if (_cache.TryGetValue(cacheKey, out List<PillarDmiResultDto> cachedData))
                {
                    return ResultResponseDto<List<PillarDmiResultDto>>.Success(
                        cachedData,
                        new List<string> { "Promoted cities fetched successfully" }
                    );
                }

                int currentYear = DateTime.Now.Year;

                var data = await _context.AiPillarStatsLast4MonthsView
                     .AsNoTracking()
                     .ToListAsync();

                var pillars = await _context.Pillars
                     .AsNoTracking()
                     .ToDictionaryAsync(x => x.PillarID);

                var result = data
                    .GroupBy(x => new { x.PillarID })
                    .Select(g =>
                    {
                        var pillar = pillars.GetValueOrDefault(g.Key.PillarID);

                        var ordered = g.OrderByDescending(x => x.MonthNo).ToList();

                        var m = ordered.Select(g => g.MonthNo).Distinct().ToList();

                        decimal p_t = m.Count > 0 ? ordered.Where(x=>x.MonthNo == m.ElementAtOrDefault(0)).Average(x=>x.ScoreProgress) : 0m;
                        decimal p_t1 = m.Count > 1 ? ordered.Where(x => x.MonthNo == m.ElementAtOrDefault(1)).Average(x => x.ScoreProgress) : 0m;
                        decimal p_t2 = m.Count > 2 ? ordered.Where(x => x.MonthNo == m.ElementAtOrDefault(2)).Average(x => x.ScoreProgress) : 0m;
                        decimal p_t3 = m.Count > 3 ? ordered.Where(x => x.MonthNo == m.ElementAtOrDefault(3)).Average(x => x.ScoreProgress) : 0m;


                        decimal dmi =
                        (
                            (0.5m * (p_t - p_t1)) +
                            (0.3m * (p_t1 - p_t2)) +
                            (0.2m * (p_t2 - p_t3))
                        ) / 20m;

                        dmi = Math.Max(-1m, Math.Min(1m, dmi));

                        return new PillarDmiResultDto
                        {
                            PillarID = g.Key.PillarID,
                            PillarName = pillar?.PillarName ?? "",
                            DisplayOrder = pillar?.DisplayOrder ?? 0,
                            Angle = dmi * 180,
                            PEMDM_t = dmi
                        };
                    })
                    .OrderBy(x => x.PillarID)
                    .ToList();

                _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                    SlidingExpiration = TimeSpan.FromMinutes(2),
                    Priority = CacheItemPriority.High
                });

                return ResultResponseDto<List<PillarDmiResultDto>>.Success(
                    result,
                    new List<string> { "Pillars Dmi fetched successfully" }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in GetPillarsDmi", ex);
                return ResultResponseDto<List<PillarDmiResultDto>>.Failure(
                    new[] { "Failed to get promoted pillars" }
                );
            }
        }
    }
}

public class CountryCityResponse
{
    public bool error { get; set; }
    public string msg { get; set; }
    public List<CountryData> data { get; set; }
}

public class CountryData
{
    public string Country { get; set; }
    public List<string> Countries { get; set; }
}

