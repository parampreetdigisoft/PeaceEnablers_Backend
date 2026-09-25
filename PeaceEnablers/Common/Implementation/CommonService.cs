using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PeaceEnablers.Common.Interface;
using PeaceEnablers.Common.Models;
using PeaceEnablers.Data;
using PeaceEnablers.Dtos.CountryDto;
using PeaceEnablers.Dtos.PillarDto;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;

namespace PeaceEnablers.Common.Implementation
{
    public class CommonService : ICommonService
    {
        #region constructor

        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _memoryCache;
        private const string PILLAR_CACHE_KEY = "PILLAR_CACHE";


        public CommonService(ApplicationDbContext context, IAppLogger appLogger, IWebHostEnvironment env, IMemoryCache memoryCache)
        {
            _context = context;
            _appLogger = appLogger;
            _env = env;
            _memoryCache = memoryCache;
        }
        #endregion
        public static string CountryScoreSummery(decimal? progress, string? countryName = "The country", int pillarCount = 23, int kpiCount = 37)
        {
            var evidenceSummaryStaringLine = $"{countryName ?? "The country"} records an overall PEM score of {progress ?? 0}, reflecting performance across {pillarCount} pillars and {kpiCount} KPIs.";

            return evidenceSummaryStaringLine;
        }

        public static string InitailLineOfExecutiveSummery(
            string evidenceSummary,
            string? immediateSituationSummary,
            decimal? progress,
            string? countryName = "The country", int pillarCount = 23, int kpiCount = 37)
        {
            immediateSituationSummary = immediateSituationSummary ?? "";

            var evidenceSummaryStaringLine= $"{countryName ?? "The country"} records an overall PEM score of {progress ?? 0}, reflecting performance across {pillarCount} pillars and {kpiCount} KPIs.";

            return immediateSituationSummary + "\n\n " + evidenceSummaryStaringLine + " " + evidenceSummary;
        }


        public async Task<List<EvaluationCountryProgressResultDto>> GetCountriesProgressAsync(int userId, int role, int year)
        {
            try
            {
                return await _context.CountryProgressResults
                 .FromSqlRaw(
                     "EXEC usp_getCountriesProgressByUserId @userID, @role, @year",
                     new SqlParameter("@userID", userId),
                     new SqlParameter("@role", role),
                     new SqlParameter("@year", year)
                 )
                 .AsNoTracking()
                 .ToListAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in Executing usp_getCountriesProgressByUserId", ex);
                return new List<EvaluationCountryProgressResultDto>();
            }
        }

        public async Task<List<CountryRankingResultDto>> GetCountriesRankings(int countryId, int year)
        {
            try
            {
                return await _context.CountryRankingResults
                 .FromSqlRaw(
                     "EXEC usp_getCountryRanking @countryId, @year",
                     new SqlParameter("@countryId", countryId),
                     new SqlParameter("@year", year)
                 )
                 .AsNoTracking()
                 .ToListAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in Executing usp_getCountryRanking", ex);
                return new List<CountryRankingResultDto>();
            }
        }

        public async Task<List<EvaluationCountryProgressHistoryResultDto>> GetCountriesProgressHistoryAsync(int userId, int role, int fromYear, int toYear)
        {
            try
            {
                return await _context.CountryProgressHistoryResults
                 .FromSqlRaw(
                     "EXEC usp_getCountriesProgressByUserIdHistory @userID, @role, @fromYear, @toYear",
                     new SqlParameter("@userID", userId),
                     new SqlParameter("@role", role),
                     new SqlParameter("@fromYear", fromYear),
                     new SqlParameter("@toYear", toYear)
                 )
                 .AsNoTracking()
                 .ToListAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in Executing usp_getCountriesProgressByUserIdHistory", ex);
                return new List<EvaluationCountryProgressHistoryResultDto>();
            }
        }
        public async Task<List<GetCountriesProgressAdminDto>> GetCountriesProgressForAdmin(int userId, int role, int year)
        {
            try
            {
                return await _context.GetCountriesProgressAdminDto
                 .FromSqlRaw("EXEC usp_getCountriesProgress_Admin @year",new SqlParameter("@year", year))
                 .AsNoTracking()
                 .ToListAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in Executing usp_getCountriesProgress_Admin", ex);
                return new List<GetCountriesProgressAdminDto>();
            }
        }
        public async Task<List<GetPillarDto>> GetPillars()
        {
            try
            {
                if (_memoryCache.TryGetValue(PILLAR_CACHE_KEY, out List<GetPillarDto> pillars))
                {
                    return pillars;
                }

                pillars = await _context.Pillars
                    .Where(x => x.IsActive && !x.IsDeleted)
                    .OrderBy(x => x.DisplayOrder)
                    .Select(x => new GetPillarDto
                    {
                        PillarID = x.PillarID,
                        PillarName = x.PillarName,
                        Description = x.Description,
                        DisplayOrder = x.DisplayOrder,
                        ImagePath = x.ImagePath,
                        Weight = x.Weight,
                        Reliability = x.Reliability,
                        PillarCode = x.PillarCode,
                        IsActive = x.IsActive,
                        QuestionCount = x.Questions.Where(x => !x.IsDeleted).Count()
                    })
                    .ToListAsync();

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromHours(1));

                _memoryCache.Set(PILLAR_CACHE_KEY, pillars, cacheOptions);

                return pillars;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetPillars", ex);
                return new List<GetPillarDto>();
            }
        }
        public void ClearPillarCache()
        {
            _memoryCache.Remove(PILLAR_CACHE_KEY);
        }
        public async Task<ResultResponseDto<bool>> RevokeCountriesPermission(List<int> countryIds, int userID, int year)
        {
            try
            {
                var date = DateTime.UtcNow;
                year = year == 0 ? date.Year : year;
                var permissionList = await _context.AIEditPermissions.Where(x => countryIds.Contains(x.CountryID) && x.Year == year).ToListAsync();
                if (permissionList == null || permissionList.Count == 0)
                    return ResultResponseDto<bool>.Failure(new[] { "Permission not found." });

                foreach (var permission in permissionList)
                {
                    permission.Status = permission.Status == AIEditPermissionStatus.PendingRequest
                    ? AIEditPermissionStatus.Rejected
                    : AIEditPermissionStatus.Revoked;
                    permission.GrantedBy = userID;
                    permission.GrantedAt = date;

                }

                var sessionIds = permissionList.Where(x => x.ActiveSessionID.HasValue).Select(x => x.ActiveSessionID);
                var sessionList = await _context.AIEditSessions.Where(x => sessionIds.Contains(x.SessionID)).ToListAsync();

                foreach (var session in sessionList)
                {
                    if (session != null && session.Status == AIEditSessionStatus.Draft)
                    {
                        session.Status = AIEditSessionStatus.Cancelled;
                        session.ReviewedBy = userID;
                        session.ReviewedAt = date;
                        session.ReviewComment = "Permission revoked by admin.";
                    }
                }

                await _context.SaveChangesAsync();
                return ResultResponseDto<bool>.Success(true, new[] { "Permission revoked." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in RevokePermission", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Failed to revoke permission." });
            }
        }

        public async Task<List<CountryPillarRankingResultDto>> GetCountriesPillarRankingAsync(int countryID = 0, int year = 0)
        {
            try
            {
                return await _context.CountryPillarRankingResults
                 .FromSqlRaw(
                     "EXEC usp_getCountryAllPillarsRanking @countryID, @year",

                     new SqlParameter("@countryID", (object?)countryID ?? DBNull.Value),
                     new SqlParameter("@year", year)
                 )
                 .AsNoTracking()
                 .ToListAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in Executing usp_getCountryAllPillarsRanking", ex);
                return new List<CountryPillarRankingResultDto>();
            }
        }
    }
}
