using PeaceEnablers.Common.Interface;
using PeaceEnablers.Data;
using PeaceEnablers.Dtos.CountryDto;
using PeaceEnablers.IServices;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace PeaceEnablers.Common.Implementation
{
    public class CommonService : ICommonService
    {
        #region constructor

        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly IWebHostEnvironment _env;
        public CommonService(ApplicationDbContext context, IAppLogger appLogger, IWebHostEnvironment env)
        {
            _context = context;
            _appLogger = appLogger;
            _env = env;
        }
        #endregion

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
    }
}
