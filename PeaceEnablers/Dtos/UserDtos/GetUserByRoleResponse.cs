using PeaceEnablers.Dtos.CountryDto;

namespace PeaceEnablers.Dtos.UserDtos
{
    public class GetUserByRoleResponse : PublicUserResponse
    {
        public List<AddUpdateCountryDto> Countries { get; set; } = new();
    }
}
