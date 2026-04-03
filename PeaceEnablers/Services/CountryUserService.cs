
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing.Charts;
using Microsoft.EntityFrameworkCore;
using PeaceEnablers.Common.Implementation;
using PeaceEnablers.Common.Models;
using PeaceEnablers.Data;
using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Dtos.AssessmentDto;
using PeaceEnablers.Dtos.CountryDto;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Dtos.kpiDto;
using PeaceEnablers.Dtos.PublicDto;
using PeaceEnablers.Enums;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;
using System.Text.RegularExpressions;
using PeaceEnablers.Dtos.CountryUserDto;

namespace PeaceEnablers.Services
{
    public class CountryUserService : ICountryUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        public CountryUserService(ApplicationDbContext context, IAppLogger appLogger)
        {
            _context = context;
            _appLogger = appLogger;
        }
        public async Task<ResultResponseDto<CountryHistoryDto>> GetCountryHistory(int userId, TieredAccessPlan tier)
        {
            try
            {
                int year = DateTime.UtcNow.Year;

                int allowedPillars = tier switch
                {
                    Enums.TieredAccessPlan.Basic => 4,
                    Enums.TieredAccessPlan.Standard => 8,
                    Enums.TieredAccessPlan.Premium => 15,
                    _ => 0
                };

                var accessibleCountryIds = await _context.PublicUserCountryMappings
                    .AsNoTracking()
                    .Where(x => x.UserID == userId && x.IsActive)
                    .Select(x => x.CountryID)
                    .ToListAsync();

                if (!accessibleCountryIds.Any())
                {
                    return ResultResponseDto<CountryHistoryDto>.Failure(new List<string> { "No countries available for user" });
                }

                var verifiedCityScores = await _context.AICountryScores
                    .AsNoTracking()
                    .Where(x =>
                        accessibleCountryIds.Contains(x.CountryID) &&
                        x.Year == year &&
                        x.IsVerified)
                    .Select(x => x.AIProgress)
                    .ToListAsync();

                var cityHistory = new CountryHistoryDto
                {
                    TotalCountry = accessibleCountryIds.Count,
                    TotalAccessCountry = accessibleCountryIds.Count,
                    ActiveCountry = verifiedCityScores.Count
                };

                if (verifiedCityScores.Any())
                {
                    cityHistory.AvgHighScore = verifiedCityScores.Max() ?? 0;
                    cityHistory.AvgLowerScore = verifiedCityScores.Min() ?? 0;
                    cityHistory.OverallVitalityScore = verifiedCityScores.Average() ?? 0;
                }

                return ResultResponseDto<CountryHistoryDto>.Success(cityHistory,new List<string> { "Get history successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in GetCityHistory", ex);
                return ResultResponseDto<CountryHistoryDto>.Failure(new[] { "There is an error, please try later" });
            }
        }


        public async Task<GetCountryQuestionHistoryResponseDto> GetCountryQuestionHistory(UserCountryRequestDto request)
        {
            try
            {
                int userId = request.UserID;
                int countryId = request.CountryID;
                int year = request.UpdatedAt.Year;

                int allowedPillars = request.Tiered switch
                {
                    Enums.TieredAccessPlan.Basic => 4,
                    Enums.TieredAccessPlan.Standard => 8,
                    Enums.TieredAccessPlan.Premium => 15,
                    _ => 0
                };

                // 🔹 Fetch accessible pillar IDs
                var accessiblePillarIds = await _context.CountryUserPillarMappings
                    .AsNoTracking()
                    .Where(x => x.UserID == userId)
                    .OrderBy(x => x.PillarID)
                    .Select(x => x.PillarID)
                    .Take(allowedPillars)
                    .ToListAsync();

                var accessiblePillarSet = accessiblePillarIds.ToHashSet();

                // 🔹 Fetch country score once
                var countryScore = await _context.AICountryScores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.CountryID == countryId && x.Year == year && x.IsVerified);

                if (countryScore == null)
                {
                    return new GetCountryQuestionHistoryResponseDto();
                }
                // 🔹 Fetch pillars
                var pillars = await _context.Pillars
                    .AsNoTracking()
                    .OrderBy(x => x.DisplayOrder)
                    .Select(p => new
                    {
                        p.PillarID,
                        p.PillarName,
                        p.ImagePath,
                        p.DisplayOrder
                    })
                    .ToListAsync();

                // 🔹 Fetch pillar scores and map for O(1) lookup
                var pillarScoreMap = await _context.AIPillarScores
                    .AsNoTracking()
                    .Where(x => x.CountryID == countryId && x.Year == year)
                    .ToDictionaryAsync(x => x.PillarID);

                // 🔹 Build DTOs
                var pillarDtos = pillars
                    .Select(p =>
                    {
                        bool isAccess = accessiblePillarSet.Contains(p.PillarID);
                        pillarScoreMap.TryGetValue(p.PillarID, out var aiScore);

                        return new CountryPillarQuestionHistoryResponseDto
                        {
                            PillarID = p.PillarID,
                            PillarName = p.PillarName,
                            ImagePath = p.ImagePath,
                            IsAccess = isAccess,
                            Score = isAccess ? aiScore?.AIProgress ?? 0 : 0,
                            ScoreProgress = isAccess ? aiScore?.AIProgress ?? 0 : 0,
                            DisplayOrder = p.DisplayOrder // optional if DTO supports it
                        };
                    })
                    .OrderByDescending(x => x.IsAccess)
                    .ThenBy(x => x.DisplayOrder)
                    .ToList();

                return new GetCountryQuestionHistoryResponseDto
                {
                    CountryID = countryId,
                    TotalAssessment = pillarScoreMap.Count,
                    Score = countryScore?.AIProgress ?? 0,
                    ScoreProgress = countryScore?.AIProgress ?? 0,
                    Pillars = pillarDtos
                };
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetCountryQuestionHistory (Optimized)", ex);
                return new GetCountryQuestionHistoryResponseDto();
            }
        }


        public async Task<PaginationResponse<CountryResponseDto>> GetCountriesAsync(PaginationRequest request)
        {
            try
            {

                int year = DateTime.UtcNow.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);

                var cityScores = from ac in _context.AICountryScores
                                 .Where(x => x.UpdatedAt >= startDate && x.UpdatedAt < endDate && x.IsVerified && x.Year == year)
                                 join pc in _context.PublicUserCountryMappings on ac.CountryID equals pc.CountryID
                                 group ac by ac.CountryID into g
                                 select new
                                 {
                                     CountryID = g.Key,
                                     Score = g.Average(x => (decimal?)x.AIProgress) ?? 0
                                 };


                // ✅ Fetch countries mapped to the user
                var query =
                    from c in _context.Countries.AsNoTracking()
                    join pc in _context.PublicUserCountryMappings.AsNoTracking()
                        on c.CountryID equals pc.CountryID

                    join s in cityScores on pc.CountryID equals s.CountryID into scores
                    from s in scores.DefaultIfEmpty() // Left join
                    where !c.IsDeleted && pc.IsActive && pc.UserID == request.UserId
                    select new CountryResponseDto
                    {
                        CountryID = c.CountryID,
                        CountryName = c.CountryName,
                        Continent = c.Continent,
                        Region = c.Region,
                        CountryCode = c.CountryCode,                        
                        Image = c.Image,
                        CreatedDate = c.CreatedDate,
                        UpdatedDate = c.UpdatedDate,
                        IsActive = c.IsActive,
                        Score = s.Score
                    };

                // ✅ Apply search filter
                if (!string.IsNullOrWhiteSpace(request.SearchText))
                {
                    string search = request.SearchText.ToLower();
                    query = query.Where(x => x.CountryName.ToLower().Contains(search) || x.Continent.ToLower().Contains(search));
                }

                // ✅ Apply ordering and pagination
                var pagedResult = await query.ApplyPaginationAsync(request);


                return pagedResult;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetCountriesAsync", ex);
                return new PaginationResponse<CountryResponseDto>();
            }
        }
        public async Task<ResultResponseDto<List<GetCountriesSubmitionHistoryResponseDto>>> GetCountriesProgressByUserId(int userID)
        {
            try
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserID == userID && x.Role == UserRole.CountryUser && !x.IsDeleted);

                if (user == null)
                    return ResultResponseDto<List<GetCountriesSubmitionHistoryResponseDto>>.Failure(new[] { "Invalid request" });

                var date = DateTime.Now;

                // Get total pillars and questions
                var pillarStats = await _context.Pillars
                    .Select(p => new { p.PillarID, QuestionsCount = p.Questions.Count })
                    .ToListAsync();

                int totalPillars = pillarStats.Count;
                int totalQuestions = pillarStats.Sum(p => p.QuestionsCount);

                // Determine allowed pillars based on tier
                var pillarPredicate = user.Tier switch
                {
                    Enums.TieredAccessPlan.Basic => 4,
                    Enums.TieredAccessPlan.Standard => 8,
                    Enums.TieredAccessPlan.Premium => 15,
                    _ => 15
                };

                var allowedPillarIds = pillarStats
                    .Where(p => p.PillarID < pillarPredicate)
                    .Select(p => p.PillarID)
                    .ToHashSet();

                // Query data with joins and projection
                var citySubmission = await (
                    from uc in _context.UserCountryMappings
                    where !uc.IsDeleted
                    join c in _context.Countries.Where(c => !c.IsDeleted && c.IsActive)
                        on uc.CountryID equals c.CountryID
                    join a in _context.Assessments.Where(a => a.IsActive && a.UpdatedAt.Year == date.Year)
                        on uc.UserCountryMappingID equals a.UserCountryMappingID into assessments
                    from a in assessments.DefaultIfEmpty()
                    select new
                    {
                        c.CountryID,
                        c.CountryName,
                        AssessmentID = (int?)a.AssessmentID,
                        PillarAssessments = a.PillarAssessments.Where(pa => allowedPillarIds.Contains(pa.PillarID)),
                        Responses = a.PillarAssessments
                                      .Where(pa => allowedPillarIds.Contains(pa.PillarID))
                                      .SelectMany(pa => pa.Responses)
                    }
                )
                .AsNoTracking()
                .ToListAsync();

                // Group by country and calculate metrics
                var result = citySubmission
                    .GroupBy(g => new { g.CountryID, g.CountryName })
                    .Select(g =>
                    {
                        var allPillars = g.SelectMany(x => x.PillarAssessments).ToList();
                        var aspIds = allPillars.Select(x => x.PillarAssessmentID).ToHashSet();
                        var allResponses = g.SelectMany(x => x.Responses).Where(r => aspIds.Contains(r.PillarAssessmentID)).ToList();

                        var scoreList = allResponses
                            .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four)
                            .Select(r => (int?)r.Score ?? 0);

                        int userCityMappingCount = g.Count();

                        return new GetCountriesSubmitionHistoryResponseDto
                        {
                            CountryID = g.Key.CountryID,
                            CountryName = g.Key.CountryName,
                            TotalAssessment = g.Select(x => x.AssessmentID).Where(id => id.HasValue).Distinct().Count(),
                            Score = allResponses.Sum(r => (int?)r.Score ?? 0),
                            TotalPillar = totalPillars * userCityMappingCount,
                            TotalAnsPillar = allPillars.Count,
                            TotalQuestion = totalQuestions * userCityMappingCount,
                            AnsQuestion = allResponses.Count,
                            ScoreProgress = scoreList.Any() ? (scoreList.Sum() * 100m) / (scoreList.Count() * 4) : 0m
                        };
                    }).ToList();

                return ResultResponseDto<List<GetCountriesSubmitionHistoryResponseDto>>.Success(result, new List<string> { "Get Cities history successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetCountriesProgressByUserId", ex);
                return ResultResponseDto<List<GetCountriesSubmitionHistoryResponseDto>>.Failure(new[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<CountryDetailsDto>> GetCountryDetails(UserCountryRequestDto userCountryRequestDto)
        {
            try
            {
                var countryId = userCountryRequestDto.CountryID;
                var userId = userCountryRequestDto.UserID;
                var year = userCountryRequestDto.UpdatedAt.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);

                // Validate country
                var country = await _context.Countries
                    .AsNoTracking()
                    .Where(x => x.CountryID == countryId && x.IsActive && !x.IsDeleted)
                    .Select(x => new { x.CountryID })
                    .FirstOrDefaultAsync();

                if (country == null)
                    return ResultResponseDto<CountryDetailsDto>.Failure(new[] { "Invalid country ID" });

                // Get user access pillars
                var accessPillarIds = await _context.CountryUserPillarMappings
                    .Where(x => x.UserID == userId)
                    .Select(x => x.PillarID)
                    .ToListAsync();

                // Get all active pillars and questions
                var allPillars = await _context.Pillars
                    .AsNoTracking()
                    .Select(p => new
                    {
                        p.PillarID,
                        p.PillarName,
                        p.DisplayOrder,
                        Questions = p.Questions.Select(q => new
                        {
                            q.QuestionID,
                            Options = q.QuestionOptions.Select(o => new { o.OptionID, o.OptionText })
                        }).ToList()
                    })
                    .ToListAsync();

                // Preload all assessments + pillar assessments + responses (flattened projection)
                var assessmentsData = await (
                    from a in _context.Assessments
                    join uc in _context.UserCountryMappings on a.UserCountryMappingID equals uc.UserCountryMappingID
                    where uc.CountryID == countryId &&
                          a.IsActive &&
                          a.UpdatedAt >= startDate &&
                          a.UpdatedAt < endDate &&
                          !uc.IsDeleted
                    select new
                    {
                        a.AssessmentID,
                        Pillars = a.PillarAssessments.Select(pa => new
                        {
                            pa.PillarID,
                            Responses = pa.Responses.Select(r => new { r.Score, r.QuestionOptionID })
                        })
                    }
                ).AsNoTracking().ToListAsync();

                var totalAssessments = assessmentsData.Count;

                if (totalAssessments == 0)
                {
                    return ResultResponseDto<CountryDetailsDto>.Success(
                        new CountryDetailsDto
                        {
                            CountryID = countryId,
                            TotalEvaluation = 0,
                            TotalPillar = allPillars.Count,
                            TotalAnsPillar = 0,
                            TotalQuestion = allPillars.SelectMany(x => x.Questions).Count(),
                            AnsQuestion = 0,
                            ScoreProgress = 0,
                            Pillars = new List<CountryPillarDetailsDto>()
                        },
                        new List<string> { "No assessments found for this country." }
                    );
                }

                // Flatten all pillar assessments and responses
                var allResponses = assessmentsData
                    .SelectMany(a => a.Pillars)
                    .SelectMany(pa => pa.Responses)
                    .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four)
                    .ToList();

                // Compute Country level stats
                var totalPillars = allPillars.Count * totalAssessments;
                var totalQuestions = allPillars.Sum(p => p.Questions.Count) * totalAssessments;
                var answeredQuestions = allResponses.Count;
                var totalScore = allResponses.Sum(r => (int?)r.Score ?? 0);
                var scoreProgress = answeredQuestions > 0
                    ? (totalScore * 100M) / (answeredQuestions * 4M)
                    : 0M;

                // Group responses by pillar
                var groupedResponses = assessmentsData
                    .SelectMany(a => a.Pillars)
                    .GroupBy(p => p.PillarID)
                    .ToDictionary(g => g.Key, g => g.SelectMany(x => x.Responses).Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four).ToList());

                var naUnknownGroup = assessmentsData
                    .SelectMany(a => a.Pillars)
                    .GroupBy(p => p.PillarID)
                    .ToDictionary(g => g.Key, g => g.SelectMany(x => x.Responses).Where(r => !r.Score.HasValue).ToList());


                // Build pillar details
                var pillarDetails = allPillars
                    .Select(p =>
                    {
                        var isAccess = accessPillarIds.Contains(p.PillarID);

                        var payload = new CountryPillarDetailsDto
                        {
                            PillarID = p.PillarID,
                            PillarName = p.PillarName,
                            DisplayOrder = p.DisplayOrder,
                            IsAccess = isAccess
                        };

                        if (isAccess)
                        {
                            groupedResponses.TryGetValue(p.PillarID, out var responses);

                            var validResponses = responses?.ToList<dynamic>() ?? new List<dynamic>();

                            var totalQuestionsForPillar = p.Questions.Count * totalAssessments;
                            var answered = validResponses.Count;
                            var totalPillarScore = validResponses.Sum(r => (int?)r.Score ?? 0);
                            var scorePct = answered > 0 ? (totalPillarScore * 100M) / (answered * 4M) : 0M;


                            naUnknownGroup.TryGetValue(p.PillarID, out var naUnknownRes);

                            var naUnknownResponse = naUnknownRes?.ToList<dynamic>() ?? new List<dynamic>();

                            var naUnknownOptionIds = naUnknownResponse.Select(r => r.QuestionOptionID).ToList();

                            var naUnknownOptions = p.Questions
                                .SelectMany(q => q.Options)
                                .Where(o => naUnknownOptionIds.Contains(o.OptionID))
                                .ToList();

                            payload.TotalQuestion = totalQuestionsForPillar;
                            payload.AnsQuestion = answered;
                            payload.TotalScore = totalPillarScore;
                            payload.ScoreProgress = scorePct;
                            payload.AvgHighScore = validResponses.Any() ? validResponses.Max(r => (int?)r.Score ?? 0) : 0;
                            payload.AvgLowerScore = validResponses.Any() ? validResponses.Min(r => (int?)r.Score ?? 0) : 0;
                            payload.TotalNA = naUnknownOptions.Count(o => o.OptionText.Contains("N/A"));
                            payload.TotalUnKnown = naUnknownOptions.Count(o => o.OptionText.Contains("Unknown"));
                        }
                        return payload;
                    })
                    .OrderByDescending(x => x.IsAccess)
                    .ThenBy(x => x.DisplayOrder)
                    .ToList();

                var countryDetails = new CountryDetailsDto
                {
                    CountryID = countryId,
                    TotalEvaluation = totalAssessments,
                    TotalPillar = totalPillars,
                    TotalAnsPillar = pillarDetails.Count(p => p.AnsQuestion > 0),
                    TotalQuestion = totalQuestions,
                    AnsQuestion = answeredQuestions,
                    TotalScore = totalScore,
                    ScoreProgress = scoreProgress,
                    AvgHighScore = pillarDetails.Any() ? pillarDetails.Max(p => p.TotalScore) : 0,
                    AvgLowerScore = pillarDetails.Any() ? pillarDetails.Min(p => p.TotalScore) : 0,
                    Pillars = pillarDetails
                };

                return ResultResponseDto<CountryDetailsDto>.Success(countryDetails, new[] { "Get country details successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in GetCountryDetails", ex);
                return ResultResponseDto<CountryDetailsDto>.Failure(new[] { "There is an error, please try later" });
            }
        }
        public async Task<ResultResponseDto<List<CountryPillarQuestionDetailsDto>>> GetCountryPillarDetails(UserCountryGetPillarInfoRequestDto userCountryRequestDto)
        {
            try
            {
                var countryId = userCountryRequestDto.CountryID;
                var pillarId = userCountryRequestDto.PillarID;
                var date = userCountryRequestDto.UpdatedAt;

                // 1. Validate country and pillar
                var country = await _context.Countries
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.CountryID == countryId && x.IsActive && !x.IsDeleted);

                if (country == null)
                    return ResultResponseDto<List<CountryPillarQuestionDetailsDto>>.Failure(new[] { "Invalid country ID" });

                var pillar = await _context.Pillars
                    .Include(p => p.Questions)
                        .ThenInclude(q => q.QuestionOptions)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.PillarID == pillarId);

                if (pillar == null)
                    return ResultResponseDto<List<CountryPillarQuestionDetailsDto>>.Failure(new[] { "Invalid pillar ID" });

                // 2. Get all assessments for this country in the given year
                var assessments = await (
                    from a in _context.Assessments
                        .Include(x => x.PillarAssessments)
                            .ThenInclude(pa => pa.Responses)
                                .ThenInclude(r => r.Question)
                    join uc in _context.UserCountryMappings.Where(x => !x.IsDeleted)
                        on a.UserCountryMappingID equals uc.UserCountryMappingID
                    where uc.CountryID == countryId && a.IsActive && a.UpdatedAt.Year == date.Year
                    select a
                ).ToListAsync();

                if (!assessments.Any())
                    return ResultResponseDto<List<CountryPillarQuestionDetailsDto>>.Failure(new[] { "No assessments found for the given country/year." });

                // 3. Flatten pillar assessments for this pillar
                var pillarAssessments = assessments
                    .SelectMany(a => a.PillarAssessments)
                    .Where(pa => pa.PillarID == pillarId)
                    .ToList();

                // 4. Flatten all responses for this pillar
                var allResponses = pillarAssessments
                    .SelectMany(pa => pa.Responses)
                    .Where(r => r != null)
                    .ToList();

                var validResponses = allResponses
                    .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four)
                    .ToList();

                // 5. Generate question-level metrics
                var result = pillar.Questions
                    .OrderBy(x => x.DisplayOrder)
                    .Select(q =>
                    {
                        var qResponses = validResponses.Where(r => r.QuestionID == q.QuestionID).ToList();
                        var totalQuestions = 1 * assessments.Count; // Each item represents one question
                        var answeredQuestions = qResponses.Count;
                        var totalScore = qResponses.Sum(r => (decimal?)r.Score ?? 0);

                        // Compute "Unknown" and "N/A" counts
                        var naUnknownIds = allResponses
                            .Where(r => r.QuestionID == q.QuestionID && !r.Score.HasValue)
                            .Select(r => r.QuestionOptionID);

                        var naUnknownOptions = q.QuestionOptions
                            .Where(opt => naUnknownIds.Contains(opt.OptionID))
                            .ToList();

                        var totalNA = naUnknownOptions.Count(opt => opt.OptionText.Contains("N/A"));
                        var totalUnknown = naUnknownOptions.Count(opt => opt.OptionText.Contains("Unknown"));

                        var scoreProgress = answeredQuestions > 0
                            ? (totalScore * 100M) / (answeredQuestions * 4M * assessments.Count)
                            : 0M;

                        return new CountryPillarQuestionDetailsDto
                        {
                            QuestionID = q.QuestionID,
                            QuestionText = q.QuestionText,
                            TotalQuestion = totalQuestions,
                            AnsQuestion = answeredQuestions,
                            TotalScore = totalScore,
                            ScoreProgress = scoreProgress,
                            AvgHighScore = qResponses.Any() ? qResponses.Max(r => (decimal?)r.Score ?? 0) : 0,
                            AvgLowerScore = qResponses.Any() ? qResponses.Min(r => (decimal?)r.Score ?? 0) : 0,
                            TotalNA = totalNA,
                            TotalUnKnown = totalUnknown
                        };
                    })
                .ToList();

                return ResultResponseDto<List<CountryPillarQuestionDetailsDto>>.Success(
                    result,
                    new List<string> { "Get country pillar question details successfully" }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in GetCountryPillarDetails", ex);
                return ResultResponseDto<List<CountryPillarQuestionDetailsDto>>.Failure(new[] { "There is an error, please try later" });
            }
        }
        public async Task<ResultResponseDto<List<PartnerCountryResponseDto>>> GetCountryUserCountries(int userID)
        {
            try
            {
                int year = DateTime.UtcNow.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);

                // Step 1️⃣: Fetch country score averages as a dictionary
                var cityScoresDict = await _context.AICountryScores
                    .Where(ar => ar.UpdatedAt >= startDate && ar.UpdatedAt < endDate && ar.IsVerified && ar.Year == year)
                    .GroupBy(ar => ar.CountryID)
                    .Select(g => new
                    {
                        CountryID = g.Key,
                        Score = g.Average(x => (decimal?)x.EvaluatorScore) ?? 0,
                        AiScore = g.Average(x => (decimal?)x.AIProgress) ?? 0
                    })
                    .ToDictionaryAsync(x => x.CountryID, x => new { x.Score, x.AiScore });

                // Step 2️⃣: Fetch countries assigned to the user
                var countries = await _context.PublicUserCountryMappings
                    .Where(x => x.IsActive && x.Country != null && !x.Country.IsDeleted && x.UserID == userID)
                    .Select(c => new PartnerCountryResponseDto
                    {
                        CountryID = c.Country.CountryID,
                        CountryName = c.Country.CountryName,
                        Continent = c.Country.Continent,
                        CountryCode = c.Country.CountryCode,
                        Region = c.Country.Region,                        
                        Image = c.Country.Image
                    })
                    .AsNoTracking()
                    .ToListAsync();

                // Step 3️⃣: Map score from dictionary (safe fallback to 0)
                foreach (var country in countries)
                {
                    if (cityScoresDict.TryGetValue(country.CountryID, out var score))
                    {
                        country.Score = score.AiScore;
                        country.AiScore = score.AiScore;
                    }
                }

                // Step 4️⃣: Sort by score descending
                var result = countries.OrderByDescending(x => x.Score).ToList();

                return ResultResponseDto<List<PartnerCountryResponseDto>>.Success(
                    result,
                    new[] { "Fetched all assigned countries successfully." }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetCityUserCities", ex);
                return ResultResponseDto<List<PartnerCountryResponseDto>>.Failure(
                    new[] { "There was an error. Please try again later." }
                );
            }
        }

        public async Task<ResultResponseDto<string>> AddCountryUserKpisCountryAndPillar(AddCountryUserKpisCountryAndPillar payload, int userId, string tierName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tierName))
                    return ResultResponseDto<string>.Failure(new[] { "Access tier information is missing. Please log in again." });

                if (!Enum.TryParse<TieredAccessPlan>(tierName, true, out var tier))
                    return ResultResponseDto<string>.Failure(new[] { "Invalid tier access. Please contact support team." });

                var tierLimits = tier switch
                {
                    TieredAccessPlan.Basic => new { Min = 5, Max = 7, Name = "Basic" },
                    TieredAccessPlan.Standard => new { Min = 8, Max = 12, Name = "Standard" },
                    TieredAccessPlan.Premium => new { Min = 13, Max = 23, Name = "Premium" },
                    _ => new { Min = 0, Max = 0, Name = "Unknown" }
                };

                if (tier != TieredAccessPlan.Premium)
                {
                    bool isValid =
                        payload.Countries.Count >= tierLimits.Min && payload.Countries.Count <= tierLimits.Max &&
                        payload.Pillars.Count >= tierLimits.Min && payload.Pillars.Count <= tierLimits.Max;

                    if (!isValid)
                    {
                        return ResultResponseDto<string>.Failure(new[]
                        {
                            $"Your {tierLimits.Name} plan allows between {tierLimits.Min} and {tierLimits.Max} selections per category (Country, Pillar, and KPI). Please adjust your selections accordingly."
                        });
                    }
                }

                //  Remove existing mappings
                var existingCities = await _context.PublicUserCountryMappings
                    .Where(m => m.UserID == userId)
                    .ToListAsync();

                var existingPillars = await _context.CountryUserPillarMappings
                    .Where(m => m.UserID == userId)
                    .ToListAsync();

                _context.PublicUserCountryMappings.RemoveRange(existingCities);
                _context.CountryUserPillarMappings.RemoveRange(existingPillars);

                var utcNow = DateTime.UtcNow;

                var newCityMappings = payload.Countries.Select(countryId => new PublicUserCountryMapping
                {
                    CountryID = countryId,
                    UserID = userId,
                    IsActive = true,
                    UpdatedAt = utcNow
                });

                var newPillarMappings = payload.Pillars.Select(pillarId => new CountryUserPillarMapping
                {
                    PillarID = pillarId,
                    UserID = userId,
                    IsActive = true,
                    UpdatedAt = utcNow
                });

                await _context.PublicUserCountryMappings.AddRangeAsync(newCityMappings);
                await _context.CountryUserPillarMappings.AddRangeAsync(newPillarMappings);

                await _context.SaveChangesAsync();

                return ResultResponseDto<string>.Success("", new[] { "Your preferences have been saved successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in AddCityUserKpisCountryAndPillar", ex);
                return ResultResponseDto<string>.Failure(new[]
                {
                    "Something went wrong while saving your selections. Please try again later."
                });
            }
        }
        public async Task<ResultResponseDto<List<GetAllKpisResponseDto>>> GetCountryUserKpi(int userId, string tierName)
        {
            try
            {
                var validPillarIds = await _context.CountryUserPillarMappings
                    .Where(x => x.IsActive && x.UserID == userId)
                    .Select(x => x.PillarID)
                    .ToListAsync();

                // Step 1: Get valid KPI IDs for this user
                var validKpiIds = await _context.AnalyticalLayerPillarMappings
                    .Where(x => validPillarIds.Contains(x.PillarID))
                    .Select(x => x.LayerID)
                    .Distinct()
                    .ToListAsync();

                if (!validKpiIds.Any())
                {
                    return ResultResponseDto<List<GetAllKpisResponseDto>>.Failure(new List<string> { "you don't have kpi access." });
                }

                // Fetch Analytical Layers that match the user's KPI access
                var result = await _context.AnalyticalLayers
                    .Where(ar => !ar.IsDeleted && validKpiIds.Contains(ar.LayerID))
                    .Select(x=>new GetAllKpisResponseDto
                    {
                        LayerID = x.LayerID,
                        LayerCode = x.LayerCode,
                        LayerName = x.LayerName
                    })
                    .ToListAsync();

                return ResultResponseDto<List<GetAllKpisResponseDto>>.Success(result);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetCityUserKpi", ex);
                return ResultResponseDto<List<GetAllKpisResponseDto>>.Failure(new List<string> { "An error occurred while fetching user KPIs." });
            }
        }

        public async Task<ResultResponseDto<CompareCountryResponseDto>> CompareCountries(CompareCountryRequestDto c, int userId, string tierName, bool applyPagination = true)
        {
            try
            {
                var year = c.UpdatedAt.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);

                var validKpiIds = new List<int>();
                if (c.Kpis.Count == 0)
                {
                    var validPillarIds = _context.CountryUserPillarMappings
                    .Where(x => x.IsActive && x.UserID == userId)
                    .Select(x => x.PillarID);

                    // Step 1: Get valid KPI IDs for this user
                    var query = _context.AnalyticalLayerPillarMappings
                        .Where(x => validPillarIds.Contains(x.PillarID))
                        .Select(x => x.LayerID)
                        .Distinct();

                    if (applyPagination)
                    {
                        var res = await query.ApplyPaginationAsync(c);
                        validKpiIds = res.Data.ToList();
                    }
                    else
                    {
                        validKpiIds = await query.ToListAsync();
                    }
                }
                else
                {
                    validKpiIds = c.Kpis;
                }


                if (!validKpiIds.Any())
                {
                    return ResultResponseDto<CompareCountryResponseDto>.Failure(new List<string> { "You don't have KPI access." });
                }

                // Step 2: Get all selected countries (even if no analytical data)
                var selectedCities = await _context.PublicUserCountryMappings
                    .Include(x=>x.Country)
                    .Where(x => c.Countries.Contains(x.CountryID) && x.UserID== userId && x.IsActive && x.Country != null && x.Country.IsActive)
                    .Select(x => new { x.Country.CountryID, x.Country.CountryName })
                    .ToListAsync();

                if (!selectedCities.Any())
                {
                    return ResultResponseDto<CompareCountryResponseDto>.Failure(new List<string> { "No valid countries found." });
                }

                // Step 3: Fetch analytical layer results for selected countries
                var analyticalResults = await _context.AnalyticalLayerResults
                    .Include(ar => ar.AnalyticalLayer)
                    .Where(x => c.Countries.Contains(x.CountryID) &&
                                ((x.LastUpdated >= startDate && x.LastUpdated < endDate) || (x.AiLastUpdated >= startDate && x.AiLastUpdated < endDate))
                                && validKpiIds.Contains(x.LayerID))
                    .Select(ar => new
                    {
                        ar.CountryID,
                        ar.LayerID,
                        ar.AnalyticalLayer.Purpose,
                        ar.AnalyticalLayer.LayerCode,
                        ar.AnalyticalLayer.LayerName,
                        ar.CalValue5,
                        ar.AiCalValue5
                    })
                    .ToListAsync();

                // Step 4: Get all distinct layers
               
                var allLayers = analyticalResults
                    .Select(x => new { x.LayerID, x.LayerCode, x.LayerName, x.Purpose })
                    .Distinct()
                    .OrderBy(x => x.LayerName)
                    .ToList();

                // Step 5: Prepare response DTO
                var response = new CompareCountryResponseDto
                {
                    Categories = new List<string>(),
                    Series = new List<ChartSeriesDto>(),
                    TableData = new List<ChartTableRowDto>()
                };

                // Initialize chart series for each country
                foreach (var country in selectedCities)
                {
                    response.Series.Add(new ChartSeriesDto
                    {
                        Name = country.CountryName,
                        AiData = new List<decimal>()
                    });
                }

                // Add Peer Country Score series
                var peerSeries = new ChartSeriesDto
                {
                    Name = "Peer Country Score",
                    AiData = new List<decimal>()
                };

                // Step 6: Build chart and table data
                foreach (var layer in allLayers)
                {
                    response.Categories.Add(layer.LayerCode);

                    // Map KPI values for each country (0 if missing)
                    var values = new Dictionary<int, List<decimal>>();

                    foreach (var country in selectedCities)
                    {
                        var value = analyticalResults
                            .FirstOrDefault(r => r.CountryID == country.CountryID && r.LayerID == layer.LayerID);

                        var evaluatedValue = Math.Round(value?.CalValue5 ?? 0, 2);
                        var aiValue = Math.Round(value?.AiCalValue5 ?? 0, 2);
                        values[country.CountryID] = new List<decimal> { evaluatedValue, aiValue };

                        //// Add to series
                        var citySeries = response.Series.First(s => s.Name == country.CountryName);

                        citySeries.AiData.Add(aiValue);
                    }

                    var aiPeerCountryScore = values.Values.Any() ? Math.Round(values.Values.Select(x=>x.Last()).Average(), 2) : 0;
                    peerSeries.AiData.Add(aiPeerCountryScore);

                    // Add table data
                    response.TableData.Add(new ChartTableRowDto
                    {
                        LayerID=layer.LayerID,
                        LayerCode = layer.LayerCode,
                        LayerName = layer.LayerName,
                        Purpose = layer.Purpose,
                        CountryValues = selectedCities.Select(c => new CountryValueDto
                        {
                            CountryID = c.CountryID,
                            CountryName = c.CountryName,
                            AiValue =  values[c.CountryID].Last()
                        }).ToList(),
                        PeerCountryScore = aiPeerCountryScore // You can rename property if needed
                    });
                }

                // Append Peer Country Score series
                response.Series.Add(peerSeries);

                return ResultResponseDto<CompareCountryResponseDto>.Success(response);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in CompareCountries", ex);
                return ResultResponseDto<CompareCountryResponseDto>.Failure(new List<string> { "An error occurred while comparing countries." });
            }
        }
        public async Task<ResultResponseDto<AiCountryPillarResponseDto>> GetAICountryPillars(AiCountryPillarRequestDto request, int userID, string tierName)
        {
            try
            {
                var currentYear = request.Year;
                var firstDate = new DateTime(currentYear, 1, 1);

                // 1. Check if country is finalized for this user (EXISTS instead of JOIN)
                var isCityFinalized = await _context.PublicUserCountryMappings
                    .AnyAsync(pum =>
                        pum.UserID == userID &&
                        pum.CountryID == request.CountryID &&
                        _context.AICountryScores.Any(ac =>
                            ac.CountryID == request.CountryID && ac.IsVerified && ac.Year == currentYear));

                if (!isCityFinalized)
                {
                    return ResultResponseDto<AiCountryPillarResponseDto>.Failure(new[] { "Country is under review process try after some time", });
                }

                var res = await _context.AIPillarScores
                    .Where(x => x.CountryID == request.CountryID && x.UpdatedAt >= firstDate && x.Year == currentYear) 
                    .Include(x => x.DataSourceCitations)
                    .ToListAsync();

                List<int> pillarIds =  await _context.CountryUserPillarMappings
                                .Where(x => x.IsActive && x.UserID == userID)
                                .Select(x => x.PillarID)
                                .Distinct()
                                .ToListAsync();
                
                var pillars = await _context.Pillars.ToListAsync();

                var result = pillars
                .GroupJoin(
                    res,
                    p => p.PillarID,
                    s => s.PillarID,
                    (pillar, scores) => new { pillar, score = scores.FirstOrDefault() }
                )
                .Select(x =>
                {
                    var isAccess = pillarIds.Count == 0 || pillarIds.Contains(x.pillar.PillarID);

                    var r = new AiCountryPillarResponse
                    {
                        PillarScoreID = x.score?.PillarScoreID ?? 0,
                        CountryID = x.score?.CountryID ?? request.CountryID,
                        CountryName = x.score?.Country?.CountryName ?? "",
                        Continent = x.score?.Country?.Continent ?? "",                        
                        PillarID = x.pillar.PillarID,
                        PillarName = x.pillar.PillarName,
                        DisplayOrder = x.pillar.DisplayOrder,
                        ImagePath = x.pillar.ImagePath,
                        IsAccess = isAccess
                    };

                    if (isAccess && x.score != null)
                    {
                        r.AIDataYear = x.score.Year;
                        r.AIScore = x.score.AIScore;
                        r.AIProgress = x.score.AIProgress;
                        r.EvidenceSummary = x.score.EvidenceSummary;
                        r.RedFlag = x.score.RedFlag;
                        r.GeographicEquityNote = x.score.GeographicEquityNote;
                        r.InstitutionalAssessment = x.score.InstitutionalAssessment;
                        r.DataGapAnalysis = x.score.DataGapAnalysis;
                        r.DataSourceCitations = x.score.DataSourceCitations;
                        r.UpdatedAt = x.score.UpdatedAt;
                    }
                    return r;
                })
                .OrderBy(x => !x.IsAccess)
                .ThenBy(x => x.DisplayOrder)
                .ToList();

                var finalResutl = new AiCountryPillarResponseDto
                {
                    Pillars = result
                };

                var resposne = ResultResponseDto<AiCountryPillarResponseDto>.Success(finalResutl, new[] { "Pillar get successfully", });

                return resposne;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetAICityPillars", ex);
                return ResultResponseDto<AiCountryPillarResponseDto>.Failure(new[] { "Error in getting pillar details", });
            }
        }
        public async Task<Tuple<string, byte[]>> ExportCompareCountries(CompareCountryRequestDto c, int userId, string tierName)
        {
            try
            {
                var result = await CompareCountries(c, userId, tierName, false);
                var data = result.Result;

                if (data == null || data.TableData == null || !data.TableData.Any())
                {
                    return new Tuple<string, byte[]>("Country_Comparison.xlsx", Array.Empty<byte>());
                }

                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("Country Comparison");

                    // =========================
                    // 🎯 REPORT HEADER (TOP)
                    // =========================
                    var countries = data.TableData.First().CountryValues;
                    int totalCols = 2 + countries.Count; // 2 fixed columns (KPI Name, Purpose) + 1 column per country (Score)

                    ws.Range(1, 1, 1, totalCols).Merge().Value = "Key Performance Integrated Report";
                    ws.Range(2, 1, 2, totalCols).Merge().Value = $"Report Year: {DateTime.Now.Year}";
                    ws.Range(3, 1, 3, totalCols).Merge().Value = $"Generated On: {DateTime.Now:dd-MMM-yyyy HH:mm}";

                    var titleRange = ws.Range(1, 1, 3, totalCols);
                    titleRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2F7D6D");
                    titleRange.Style.Font.FontColor = XLColor.White;
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    ws.Row(1).Height = 28;
                    ws.Row(2).Height = 22;
                    ws.Row(3).Height = 22;

                    // =========================
                    // 📊 MULTI-ROW TABLE HEADER
                    // =========================
                    int row = 5;
                    int col = 1;

                    // KPI Name
                    ws.Range(row, col, row + 1, col).Merge().Value = "KPI Name";
                    col++;

                    // Purpose
                    ws.Range(row, col, row + 1, col).Merge().Value = "Purpose";
                    col++;

                    // Dynamic Cities (only Score)
                    foreach (var country in countries)
                    {
                        ws.Range(row, col, row + 1, col).Merge().Value = country.CountryName;
                        ws.Cell(row + 1, col).Value = "Score";
                        col++;
                    }

                    // Style header (both rows)
                    var headerRange = ws.Range(row, 1, row + 1, totalCols);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.FontColor = XLColor.White;
                    headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2F7D6D");
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    // =========================
                    // 📄 DATA ROWS
                    // =========================
                    row += 2;
                    int startDataRow = row;

                    foreach (var kpi in data.TableData)
                    {
                        col = 1;

                        ws.Cell(row, col++).Value = $"{kpi.LayerName} ({kpi.LayerCode})";

                        var cleanPurpose = StripHtml(kpi.Purpose);
                        var purposeCell = ws.Cell(row, col++);
                        purposeCell.Value = string.IsNullOrEmpty(cleanPurpose) ? "NA" : cleanPurpose;

                        if (!string.IsNullOrEmpty(cleanPurpose))
                        {
                            var comment = purposeCell.GetComment();
                            comment.AddText(cleanPurpose);
                            comment.Visible = false;
                        }

                        foreach (var country in kpi.CountryValues)
                        {
                            ws.Cell(row, col++).Value = country.AiValue; // Only AI value
                        }

                        row++;
                    }

                    int endDataRow = row - 1;

                    // =========================
                    // 🎨 STYLING
                    // =========================
                    ws.Column(1).Width = 30;  // KPI Name
                    ws.Column(2).Width = 55;  // Purpose

                    for (int i = 3; i <= totalCols; i++)
                    {
                        ws.Column(i).Width = 18;
                    }

                    ws.Column(2).Style.Alignment.WrapText = true;
                    ws.Column(2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                    ws.Columns(3, totalCols).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Rows().AdjustToContents();
                    ws.SheetView.FreezeRows(6);

                    var dataRange = ws.Range(5, 1, endDataRow, totalCols);
                    dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                    for (int i = startDataRow; i <= endDataRow; i++)
                    {
                        if (i % 2 == 0)
                        {
                            ws.Range(i, 1, i, totalCols).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
                        }
                    }

                    ws.Range(6, 1, 6, totalCols).SetAutoFilter();

                    // =========================
                    // 📄 SHEET 2: KPI Details
                    // =========================
                    var ws2 = workbook.Worksheets.Add("KPI Details");
                    int r = 1;

                    ws2.Cell(r, 1).Value = "KPI Name";
                    ws2.Cell(r, 2).Value = "Full Purpose";

                    var header2 = ws2.Range(r, 1, r, 2);
                    header2.Style.Font.Bold = true;
                    header2.Style.Font.FontColor = XLColor.White;
                    header2.Style.Fill.BackgroundColor = XLColor.FromHtml("#2F7D6D");

                    r++;

                    foreach (var kpi in data.TableData)
                    {
                        ws2.Cell(r, 1).Value = $"{kpi.LayerName} ({kpi.LayerCode})";
                        ws2.Cell(r, 2).Value = StripHtml(kpi.Purpose);
                        r++;
                    }

                    ws2.Column(1).Width = 40;
                    ws2.Column(2).Width = 100;
                    ws2.Column(2).Style.Alignment.WrapText = true;
                    ws2.Rows().AdjustToContents();
                    ws2.SheetView.FreezeRows(1);

                    // =========================
                    // 📤 EXPORT
                    // =========================
                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        return new Tuple<string, byte[]>("City_Comparison.xlsx", stream.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in ExportCompareCountries", ex);
                return new Tuple<string, byte[]>("", Array.Empty<byte>());
            }
        }
        private string StripHtml(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            return Regex.Replace(input, "<.*?>", string.Empty).Trim();
        }
    }
}
