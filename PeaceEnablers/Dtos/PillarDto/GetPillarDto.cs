namespace PeaceEnablers.Dtos.PillarDto
{
    public class GetPillarDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public string Description { get; set; }
        public int DisplayOrder { get; set; }
        public string? ImagePath { get; set; }
        public double Weight { get; set; } = 1.0;
        public bool Reliability { get; set; } = true;
        public string? PillarCode { get; set; }
        public bool IsActive { get; set; } = true;
        public int QuestionCount { get; set; }
    }
}
