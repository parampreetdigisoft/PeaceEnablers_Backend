using PeaceEnablers.Common.Models;
using PeaceEnablers.Dtos.AssessmentDto;
using PeaceEnablers.Dtos.CountryDto;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Models;

namespace PeaceEnablers.IServices
{
    public interface ICountryService
    {
        Task<PaginationResponse<UserCountryMappingResponseDto>> GetCountriesAsync(PaginationRequest request, UserRole userRole);
        Task<ResultResponseDto<List<UserCountryMappingResponseDto>>> getAllCountryByUserId(int userId, UserRole userRole);
        Task<ResultResponseDto<Country>> GetByIdAsync(int id);
        Task<ResultResponseDto<string>> AddBulkCountryAsync(BulkAddCountryDto q, string image = "");
        Task<ResultResponseDto<Country>> EditCountryAsync(int id, AddUpdateCountryDto q);
        Task<ResultResponseDto<bool>> DeleteCountryAsync(int id);
        Task<ResultResponseDto<object>> AssingCountryToUser(int userId, int countryId, int AssignedByUserId);
        Task<ResultResponseDto<object>> EditAssingCountry(int id,int userId, int countryId, int AssignedByUserId);
        Task<ResultResponseDto<object>> UnAssignCountry(UserCountryUnMappingRequestDto requestDto);
        Task<ResultResponseDto<List<UserCountryMappingResponseDto>>> GetCountryByUserIdForAssessment(int userId);
        Task<ResultResponseDto<CountryHistoryDto>> GetCountryHistory(int userID, DateTime updatedA, UserRole userRole);
        Task<ResultResponseDto<List<GetCountriesSubmitionHistoryResponseDto>>> GetCountriesProgressByUserId(int userID, DateTime updateAt, UserRole userRole);
        Task<ResultResponseDto<string>> AddUpdateCountry(AddUpdateCountryDto q);
        Task<ResultResponseDto<List<UserCountryMappingResponseDto>>> getAllCountryByLocation(GetNearestCountryRequestDto r);
        Task<ResultResponseDto<List<UserCountryMappingResponseDto>>> GetAiAccessCountry(int userId, UserRole userRole);        
        Task<ResultResponseDto<byte[]>> ExportCountries(ExportCountryWithOptionDto request, int userId, UserRole userRole);
    }
}
