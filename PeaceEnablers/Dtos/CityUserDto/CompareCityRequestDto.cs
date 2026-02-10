using PeaceEnablers.Dtos.CommonDto;

namespace PeaceEnablers.Dtos.CityUserDto
{
    public class CompareCityRequestDto : PaginationRequest
    {
        public List<int> Cities { get; set; }
        public List<int> Kpis { get; set; } = new();
        public DateTime UpdatedAt { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);
    }

}
