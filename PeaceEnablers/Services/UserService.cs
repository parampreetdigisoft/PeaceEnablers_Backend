using Microsoft.EntityFrameworkCore;
using PeaceEnablers.Common.Implementation;
using PeaceEnablers.Common.Models;
using PeaceEnablers.Data;
using PeaceEnablers.Dtos.AssessmentDto;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Dtos.CountryDto;
using PeaceEnablers.Dtos.UserDtos;
using PeaceEnablers.Enums;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;
using System.Linq.Expressions;

namespace PeaceEnablers.Services
{
    public class UserService : IUserService
    {
        private readonly IAppLogger _appLogger;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        public UserService(ApplicationDbContext context, IAppLogger appLogger, IWebHostEnvironment env)
        {
            _context = context;
            _appLogger = appLogger;
            _env = env;
        }
        public User? GetByEmail(string email)
        {
            return _context.Users.FirstOrDefault(u => u.Email == email);
        }
        public async Task<PaginationResponse<GetUserByRoleResponse>> GetUserByRoleWithAssignedCountry(GetUserByRoleRequestDto request)
        {
            try
            {
                var currentUser = _context.Users.First(u => u.UserID == request.UserID);

                var filteredMappings =
                    _context.UserCountryMappings
                        .Where(x => !x.IsDeleted &&
                               (x.AssignedByUserId == request.UserID || currentUser.Role == UserRole.Admin));

                Expression<Func<User, bool>> predicate = currentUser.Role switch
                {
                    UserRole.Admin => x => !x.IsDeleted && (request.GetUserRole.HasValue
                                        ? x.Role == request.GetUserRole
                                        : (x.Role == UserRole.Evaluator || x.Role == UserRole.CountryUser)),
                    _ => x => !x.IsDeleted && x.Role == UserRole.Evaluator
                };

                var query =
                    from u in _context.Users.Where(predicate)
                    from uc in filteredMappings
                                .Where(m => m.UserID == u.UserID)
                                .Take(1).DefaultIfEmpty()
                    from ab in _context.Users
                                .Where(p => uc != null && p.UserID == uc.AssignedByUserId)
                                .DefaultIfEmpty()
                    select new GetUserByRoleResponse
                    {
                        UserID = u.UserID,
                        FullName = u.FullName,
                        Email = u.Email,
                        Phone = u.Phone,
                        Role = u.Role.ToString(),
                        CreatedBy = uc != null ? uc.AssignedByUserId : null,
                        IsDeleted = u.IsDeleted,
                        IsEmailConfirmed = u.IsEmailConfirmed,
                        CreatedAt = u.CreatedAt,
                        CreatedByName = ab != null ? ab.FullName : null,
                        Tier = u.Tier,
                        Countries = new List<AddUpdateCountryDto>(),
                        Pillars = new List<int>()  // initialize empty list
                    };

                var response = await query.ApplyPaginationAsync(
                    request,
                    x => string.IsNullOrEmpty(request.SearchText) ||
                         x.Email.Contains(request.SearchText) ||
                         x.FullName.Contains(request.SearchText));

                var userIds = response.Data.Select(x => x.UserID).Distinct().ToList();

                if (request.GetUserRole == UserRole.CountryUser)
                {
                    // Fetch countries from PublicUserCountryMappings
                    var countryMap = await _context.PublicUserCountryMappings
                        .Where(x => x.IsActive && userIds.Contains(x.UserID))
                        .Join(_context.Countries,
                            cm => cm.CountryID,
                            c => c.CountryID,
                            (cm, c) => new { cm.UserID, Country = new AddUpdateCountryDto { CountryID = c.CountryID, CountryName = c.CountryName, Region = c.Region, Continent = c.Continent } })
                        .ToListAsync();

                    // Fetch pillar IDs from CountryUserPillarMappings
                    var pillarMap = await _context.CountryUserPillarMappings
                        .Where(x => x.IsActive && userIds.Contains(x.UserID))
                        .Select(x => new { x.UserID, x.PillarID })
                        .ToListAsync();

                    var countriesGrouped = countryMap.GroupBy(x => x.UserID)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.Country).ToList());

                    var pillarsGrouped = pillarMap.GroupBy(x => x.UserID)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.PillarID).ToList());

                    foreach (var item in response.Data)
                    {
                        countriesGrouped.TryGetValue(item.UserID, out var countries);
                        pillarsGrouped.TryGetValue(item.UserID, out var pillars);

                        item.Countries = countries ?? new List<AddUpdateCountryDto>();
                        item.Pillars = pillars ?? new List<int>();
                    }
                }
                else
                {
                    // For Evaluator / Analyst, keep your existing logic for countries
                    var countryMap = await _context.UserCountryMappings
                        .Where(x => !x.IsDeleted &&
                               userIds.Contains(x.UserID) &&
                               (x.AssignedByUserId == request.UserID || currentUser.Role == UserRole.Admin))
                        .Join(_context.Countries,
                            cm => cm.CountryID,
                            c => c.CountryID,
                            (cm, c) => new { cm.UserID, Country = new AddUpdateCountryDto { CountryID = c.CountryID, CountryName = c.CountryName, Region = c.Region, Continent = c.Continent } })
                        .ToListAsync();

                    var countriesGrouped = countryMap.GroupBy(x => x.UserID)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.Country).ToList());

                    foreach (var item in response.Data)
                    {
                        countriesGrouped.TryGetValue(item.UserID, out var countries);
                        item.Countries = countries ?? new List<AddUpdateCountryDto>();
                        item.Pillars = new List<int>(); // no pillars for other roles
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetUserByRoleWithAssignedCity", ex);
                return new PaginationResponse<GetUserByRoleResponse>();
            }
        }
        public async Task<ResultResponseDto<List<PublicUserResponse>>> GetEvaluatorByAnalyst(GetAssignUserDto request)
        {
            try
            {
                var query =
                    from uc in _context.UserCountryMappings
                    where !uc.IsDeleted
                          && uc.AssignedByUserId == request.UserID
                          && (!request.SearchedUserID.HasValue || uc.UserID == request.SearchedUserID.Value)
                          && (!request.CountryID.HasValue || uc.CountryID == request.CountryID.Value)
                    join u in _context.Users
                        .Where(x => !x.IsDeleted)
                        on uc.UserID equals u.UserID
                    select new PublicUserResponse
                    {
                        UserID = u.UserID,
                        FullName = u.FullName,
                        Email = u.Email,
                        Phone = u.Phone,
                        Role = u.Role.ToString(),
                        CreatedBy = uc.AssignedByUserId,
                        IsDeleted = u.IsDeleted,
                        IsEmailConfirmed = u.IsEmailConfirmed,
                        CreatedAt = u.CreatedAt
                    };

                var users = await query
                    .Distinct()
                    .OrderBy(x => x.FullName)
                    .ToListAsync();

                return ResultResponseDto<List<PublicUserResponse>>
                    .Success(users, new[] { "User fetched successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetEvaluatorByAnalyst", ex);
                return ResultResponseDto<List<PublicUserResponse>>
                    .Failure(new[] { "There is an error, please try later" });
            }
        }

        public async Task<ResultResponseDto<List<GetAssessmentResponseDto>>> GetUsersAssignedToCountry(int countryId)
        {
            try
            {
                var year = DateTime.Now.Year;
                var query =
                from u in _context.Users
                where !u.IsDeleted
                join uc in _context.UserCountryMappings
                        .Where(x => !x.IsDeleted && x.CountryID == countryId)
                    on u.UserID equals uc.UserID
                join c in _context.Countries.Where(x => !x.IsDeleted)
                    on uc.CountryID equals c.CountryID
                join createdBy in _context.Users.Where(x => !x.IsDeleted)
                    on uc.AssignedByUserId equals createdBy.UserID into createdByUser
                from createdBy in createdByUser.DefaultIfEmpty()

                    // LEFT JOIN to Assessments
                join a in _context.Assessments
                        .Include(q => q.PillarAssessments)
                            .ThenInclude(q => q.Responses).Where(x=>x.IsActive && x.CreatedAt.Year == year)
                    on uc.UserCountryMappingID equals a.UserCountryMappingID into userAssessment
                from a in userAssessment.DefaultIfEmpty()

                select new GetAssessmentResponseDto
                {
                    AssessmentID = a != null ? a.AssessmentID : 0,
                    UserCountryMappingID = uc.UserCountryMappingID,
                    CreatedAt = a != null ? a.CreatedAt : null,
                    CountryID = c.CountryID,
                    CountryName = c.CountryName,
                    Continent = c.Continent,
                    UserID = u.UserID,
                    UserName = u.FullName,
                    Score = a != null
                        ? a.PillarAssessments.SelectMany(x => x.Responses)
                            .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four)
                            .Sum(r => (int?)r.Score ?? 0)
                        : 0,
                    AssignedByUser = createdBy != null ? createdBy.FullName : "",
                    AssignedByUserId = createdBy != null ? createdBy.UserID : 0,
                    AssessmentYear = a != null ? a.UpdatedAt.Year : 0,
                    AssessmentPhase = a != null ? a.AssessmentPhase : null
                };



                var users = await query.Distinct().ToListAsync();

                return ResultResponseDto<List<GetAssessmentResponseDto>>.Success(users, new[] { "user get successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetUsersAssignedToCity", ex);
                return ResultResponseDto<List<GetAssessmentResponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<UpdateUserResponseDto>> GetUserInfo(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return ResultResponseDto<UpdateUserResponseDto>.Failure(new List<string>() { "Invalid request " });

                var response = new UpdateUserResponseDto
                {
                    UserID = user.UserID,
                    FullName = user.FullName,
                    Phone = user.Phone,
                    Email = user.Email,
                    ProfileImagePath = user?.ProfileImagePath,
                    Is2FAEnabled = user?.Is2FAEnabled ?? false,
                    Tier = user?.Tier ?? Enums.TieredAccessPlan.Pending
                };

                return ResultResponseDto<UpdateUserResponseDto>.Success(response, new List<string> { "Updated successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure UpdateUser", ex);
                return ResultResponseDto<UpdateUserResponseDto>.Failure(new string[] { "There is an error please try later" });
            }
        }
    }
}