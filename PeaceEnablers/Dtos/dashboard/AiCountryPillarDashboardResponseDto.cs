namespace PeaceEnablers.Dtos.dashboard
{
    public class AiCountryPillarDashboardResponseDto
    {
        public int CountryID { get; set; }
        public string CountryName { get; set; }
        public decimal EvaluationValue { get; set; }
        public decimal AiValue { get; set; }
        public List<CountryPillarDashboardPillarValueDto> Pillars { get; set; } = new List<CountryPillarDashboardPillarValueDto>();
    }

    public class CountryPillarDashboardPillarValueDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public int DisplayOrder { get; set; }
        public decimal EvaluationValue { get; set; }
        public decimal AiValue { get; set; }
    }
}
