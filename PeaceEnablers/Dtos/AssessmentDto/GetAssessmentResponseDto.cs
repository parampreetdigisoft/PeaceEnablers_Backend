using PeaceEnablers.Models;

namespace PeaceEnablers.Dtos.AssessmentDto
{
    public class GetAssessmentResponseDto
    {
        public int AssessmentID { get; set; }
        public int UserCountryMappingID { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int CountryID { get; set; }
        public string Continent { get; set; }
        public string CountryName { get; set; }
        public bool IsActive { get; set; } = true;
        public int UserID { get; set; }
        public string UserName { get; set; }
        public decimal Score { get; set; }
        public string AssignedByUser { get; set; }
        public int AssignedByUserId { get; set; }
        public int AssessmentYear { get; set; } 
        public AssessmentPhase? AssessmentPhase { get; set; }
    }

    public class GetCountryAssessmentResponseDto : GetAssessmentResponseDto
    {
        public int TotalUnknown { get; set; }
        public int TotalNA { get; set; }
    }
}
