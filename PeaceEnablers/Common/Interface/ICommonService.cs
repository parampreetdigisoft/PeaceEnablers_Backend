using PeaceEnablers.Common.Models;
using PeaceEnablers.Dtos.CountryDto;
using PeaceEnablers.Dtos.PillarDto;
using PeaceEnablers.Models;

namespace PeaceEnablers.Common.Interface
{
    public interface ICommonService
    {
        Task<List<EvaluationCountryProgressResultDto>> GetCountriesProgressAsync(int userId,int role, int year);
        Task<List<EvaluationCountryProgressHistoryResultDto>> GetCountriesProgressHistoryAsync(int userId, int role, int fromYear, int toYear);
        Task<List<GetCountriesProgressAdminDto>> GetCountriesProgressForAdmin(int userId, int role, int year);
        Task<List<CountryRankingResultDto>> GetCountriesRankings(int countryId, int year);
        Task<List<GetPillarDto>> GetPillars();
        void ClearPillarCache();
        Task<ResultResponseDto<bool>> RevokeCountriesPermission(List<int> countryIds, int userID, int year);
        Task<List<CountryPillarRankingResultDto>> GetCountriesPillarRankingAsync(int countryID = 0, int year = 0);

    }
}
