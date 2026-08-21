namespace PeaceEnablers.Dtos.kpiDto
{
    public class AnalyticalLayerPillarMappingDto
    {
        public int AnalyticalLayerPillarMappingID { get; set; }
        public int LayerID { get; set; }
        public string? LayerName { get; set; }
        public int PillarID { get; set; }
        public string? PillarCode { get; set; }
        public string PillarName { get; set; } = string.Empty;
        public int? CategoryNumber { get; set; }
    }
}
