using PeaceEnablers.Common.Models;

namespace PeaceEnablers.Dtos.AiDto
{
    public class KpiSummaryAiRequest
    {
        public string? CountryName { get; set; }
        public string LayerName { get; set; } = string.Empty;
        public string LayerCode { get; set; } = string.Empty;
        public string? Purpose { get; set; }
        public decimal? ManualScore { get; set; }
        public decimal? AiScore { get; set; }
        public string? ManualCondition { get; set; }
        public string? AiCondition { get; set; }
        public List<KpiInterpretationBandAiDto> InterpretationBands { get; set; } = new();
        public string? CategoryDetails { get; set; }
    }

    public class KpiInterpretationBandAiDto
    {
        public decimal? MinRange { get; set; }
        public decimal? MaxRange { get; set; }
        public string? Condition { get; set; }
        public string? Descriptor { get; set; }
        public string? StrategicAction { get; set; }
    }

    public class KpiSummaryAiResponse : IAiServiceResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public KpiSummaryAiResultDto? Result { get; set; }
    }

    public class KpiSummaryAiResultDto
    {
        public string Summary { get; set; } = string.Empty;
        public string? ScoreInterpretation { get; set; }
        public List<string> KeyTakeaways { get; set; } = new();
        public string? Outlook { get; set; }
    }
}
