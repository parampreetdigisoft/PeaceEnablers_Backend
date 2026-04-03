using PeaceEnablers.Dtos.CountryDto;

namespace PeaceEnablers.Common.Interface
{
    public interface ICommonService
    {
        /// <summary>
        /// Based on user role it will return pillar wise Manual progress and Ai progress Score
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="role"></param>
        /// <param name="year"></param>
        /// <returns></returns>
        public Task<List<EvaluationCountryProgressResultDto>> GetCountriesProgressAsync(int userId,int role, int year);
        public Task<List<EvaluationCountryProgressHistoryResultDto>> GetCountriesProgressHistoryAsync(int userId, int role, int fromYear, int toYear);
        public Task<List<GetCountriesProgressAdminDto>> GetCountriesProgressForAdmin(int userId, int role, int year);
    }
}
