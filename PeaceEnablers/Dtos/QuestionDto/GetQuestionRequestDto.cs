using PeaceEnablers.Dtos.CommonDto;

namespace PeaceEnablers.Dtos.QuestionDto
{
    public class GetQuestionRequestDto : PaginationRequest
    {
        public int? PillarID { get; set; }
    }
}
