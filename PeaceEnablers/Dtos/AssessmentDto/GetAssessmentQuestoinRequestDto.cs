using PeaceEnablers.Dtos.CommonDto;

namespace PeaceEnablers.Dtos.AssessmentDto
{
    public class GetAssessmentQuestoinRequestDto : PaginationRequest
    {
        public int AssessmentID { get; set; } 
        public int? PillarID { get; set; }
    }
}
