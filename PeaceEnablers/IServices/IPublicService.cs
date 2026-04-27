using PeaceEnablers.Common.Models;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Dtos.PublicDto;

namespace PeaceEnablers.IServices
{
    public interface IPublicService
    {
        Task<ResultResponseDto<List<PartnerCountryResponseDto>>> getAllCountries();
        Task<ResultResponseDto<PartnerCountryFilterResponse>> GetPartnerCountriesFilterRecord();
        Task<ResultResponseDto<List<PillarResponseDto>>> GetAllPillarAsync();
        Task<PaginationResponse<PartnerCountryResponseDto>> GetPartnerCountries(PartnerCountryRequestDto r);
        Task<CountryCityResponse> GetCountriesAndCountries_WithStaleSupport();
        Task<ResultResponseDto<List<PromotedPillarsResponseDto>>> GetPromotedCountries();
        Task<ResultResponseDto<List<PillarDmiResultDto>>> GetPillarsDmi();
    }
}
