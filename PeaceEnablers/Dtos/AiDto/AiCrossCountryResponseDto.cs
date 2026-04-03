namespace PeaceEnablers.Dtos.AiDto
{
    public class AiCrossCountryResponseDto
    {
        public List<string> Categories { get; set; } = new List<string>();
        public List<CrossCountryChartSeriesDto> Series { get; set; } = new List<CrossCountryChartSeriesDto>();
        public List<CrossCountryChartTableRowDto> TableData { get; set; } = new List<CrossCountryChartTableRowDto>();
    }

    public class CrossCountryChartSeriesDto
    {
        public string Name { get; set; }
        public List<decimal> Data { get; set; }
    }

    public class CrossCountryChartTableRowDto
    {
        public int CountryID { get; set; }
        public string CountryName { get; set; }
        public decimal Value { get; set; }
        public List<CrossCountryPillarValueDto> PillarValues { get; set; } = new List<CrossCountryPillarValueDto>();
    }

    public class CrossCountryPillarValueDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public int DisplayOrder { get; set; }
        public decimal Value { get; set; }
        public bool IsAccess { get; set; }
    }
}
