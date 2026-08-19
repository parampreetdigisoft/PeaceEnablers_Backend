namespace PeaceEnablers.Dtos.PillarDto
{
    public class AddPillarDto
    {
        public string? PillarName { get; set; }
        public string? Description { get; set; }
        public double Weight { get; set; } = 1.0;
        public bool Reliability { get; set; } = true;
        public IFormFile? ImageFile { get; set; }
        public string? KpiLayerIds { get; set; }
        public string? PillarCode { get; set; }
        public int DisplayOrder { get; set; }
    }
}
