using PeaceEnablers.Common.Models;
using PeaceEnablers.Dtos.AiDto;

namespace PeaceEnablers.IServices
{
    public interface IAiCurrentJobsService
    {
        Task<ResultResponseDto<AiCurrentJobsHealthDto>> GetHealthAsync();
        Task<ResultResponseDto<AiCurrentJobsResultDto>> GetJobsAsync(AiCurrentJobsFilterDto filter);
        Task<ResultResponseDto<AiCurrentJobDto>> GetJobAsync(string jobId);
        Task<ResultResponseDto<object>> GetCountryStatusAsync(int countryId);
        Task<ResultResponseDto<AiCurrentJobDto>> CancelJobAsync(string jobId);
    }
}
