namespace PeaceEnablers.Models
{
    public class AIUserCountryMapping // Ai country for evaluator
    {
        public int AIUserCountryMappingID { get; set; }

        public int CountryID { get; set; }

        public int UserID { get; set; }

        public int? AssignBy { get; set; }

        public bool IsActive { get; set; } = false;

        public string? Comment { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

