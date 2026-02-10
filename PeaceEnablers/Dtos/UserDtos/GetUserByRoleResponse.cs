using PeaceEnablers.Dtos.CityDto;

namespace PeaceEnablers.Dtos.UserDtos
{
    public class GetUserByRoleResponse : PublicUserResponse
    {
        public List<AddUpdateCityDto> cities { get; set; } = new();
    }
}
