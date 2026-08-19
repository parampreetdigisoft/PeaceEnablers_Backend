namespace PeaceEnablers.Dtos.kpiDto
{
    public class SummarizeKpiRequestDto
    {
        public int LayerResultID { get; set; }
    }

    public class SummarizeKpiResponseDto
    {
        public string Summary { get; set; } = string.Empty;
        public string? ScoreInterpretation { get; set; }
        public List<string> KeyTakeaways { get; set; } = new();
        public string? Outlook { get; set; }
    }
}
