namespace PeaceEnablers.Models
{
    public class Pillar
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public string Description { get; set; }
        public int DisplayOrder { get; set; }
        public string ImagePath { get; set; }
        public double Weight { get; set; } = 1.0; 
        public bool Reliability { get; set; } = true; 
        public string? PillarCode { get; set; } 
        public ICollection<Question> Questions { get; set; }
    }
} 