using PeaceEnablers.Dtos.CommonDto;

namespace PeaceEnablers.Dtos.CountryDto
{
    public class CountryPaginationRequest: PaginationRequest
    {
        public int? CountryID { get; set; }
    }
}
