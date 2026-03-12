using PeaceEnablers.Common.Models;
using PeaceEnablers.Dtos.AssessmentDto;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Dtos.UserDtos;
using PeaceEnablers.Models;

namespace PeaceEnablers.IServices
{
    public interface IUserService
    {
        User GetByEmail(string email);
        Task<PaginationResponse<GetUserByRoleResponse>> GetUserByRoleWithAssignedCity(GetUserByRoleRequestDto requestDto);
        Task<ResultResponseDto<List<PublicUserResponse>>> GetEvaluatorByAnalyst(GetAssignUserDto requestDto);
        Task<ResultResponseDto<List<GetAssessmentResponseDto>>> GetUsersAssignedToCity(int cityId);
        Task<ResultResponseDto<UpdateUserResponseDto>> GetUserInfo(int userId);

    }
} 