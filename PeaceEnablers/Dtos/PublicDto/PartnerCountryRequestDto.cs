using PeaceEnablers.Dtos.CommonDto;

namespace PeaceEnablers.Dtos.PublicDto
{
    public class PartnerCountryRequestDto : PaginationRequest
    {
        public string? Country { get; set; }
        public int? CountryID { get; set; }
        public string? Region { get; set; }
        public int? PillarID { get; set; }
    }
    
}
