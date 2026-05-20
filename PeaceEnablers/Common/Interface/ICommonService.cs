using PeaceEnablers.Dtos.CountryDto;

namespace PeaceEnablers.Common.Interface
{
    public interface ICommonService
    {
        Task<List<EvaluationCountryProgressResultDto>> GetCountriesProgressAsync(int userId,int role, int year);
        Task<List<EvaluationCountryProgressHistoryResultDto>> GetCountriesProgressHistoryAsync(int userId, int role, int fromYear, int toYear);
        Task<List<GetCountriesProgressAdminDto>> GetCountriesProgressForAdmin(int userId, int role, int year);
        Task<List<CountryRankingResultDto>> GetCountriesRankings(int countryId, int year);
    }
}
