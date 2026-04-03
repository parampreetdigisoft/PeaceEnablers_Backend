using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Models;

namespace PeaceEnablers.Dtos.AssessmentDto
{
    public class GetAssessmentRequestDto : PaginationRequest
    {
        public int? SubUserID { get; set; } //Means admin or analyst can see result of a user that they has permission
        public int? CountryID { get; set; }
        public UserRole? Role { get; set; }
        public DateTime UpdatedAt { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);
    }
}
    