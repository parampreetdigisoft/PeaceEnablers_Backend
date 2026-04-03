using AssessmentPlatform.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PeaceEnablers.Backgroundjob;
using PeaceEnablers.Common.Implementation;
using PeaceEnablers.Common.Interface;
using PeaceEnablers.Common.Models;
using PeaceEnablers.Common.Models.settings;
using PeaceEnablers.Data;
using PeaceEnablers.Dtos.AssessmentDto;
using PeaceEnablers.Dtos.CountryDto;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;
using System.Data;
using System.Linq.Expressions;

namespace PeaceEnablers.Services
{
    public class CountryService : ICountryService
    {
        #region constructor

        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly IWebHostEnvironment _env;
        private readonly ICommonService _commonService;
        private readonly Download _download;
        private readonly AppSettings _appSettings;
        public CountryService(ApplicationDbContext context, IAppLogger appLogger, IWebHostEnvironment env, ICommonService commonService, Download download, IOptions<AppSettings> appSettings)
        {
            _context = context;
            _appLogger = appLogger;
            _env = env;
            _commonService = commonService;
            _download = download;
            _appSettings = appSettings.Value;
        }

        #endregion

        #region  methods Implementations
        public async Task<ResultResponseDto<string>> AddUpdateCountry(AddUpdateCountryDto q)
        {
            try
            {
                string image = string.Empty;
                if (q.ImageFile != null)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "assets/cities");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    // ?? Remove old image if exists
                    if (!string.IsNullOrEmpty(q.ImageUrl))
                    {
                        string oldFilePath = Path.Combine(_env.WebRootPath, q.ImageUrl.TrimStart('/'));
                        if (File.Exists(oldFilePath))
                        {
                            File.Delete(oldFilePath);
                        }
                    }

                    // Save new image
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(q.ImageFile.FileName);
                    string filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await q.ImageFile.CopyToAsync(stream);
                    }

                    image = "/assets/cities/" + fileName;
                }
                if(q.CountryID > 0)
                {
                    var existCity = await _context.Countries.FirstOrDefaultAsync(x => x.IsActive && !x.IsDeleted && q.CountryName == x.CountryName && x.Continent == q.Continent && x.CountryID != q.CountryID);
                    if (existCity != null)
                    {
                        return ResultResponseDto<string>.Failure(new string[] { "Country already exists" });
                    }

                    var existing = await _context.Countries.FindAsync(q.CountryID);
                    if (existing == null) return ResultResponseDto<string>.Failure(new string[] { "Country not exists" });
                    existing.CountryName = q.CountryName;
                    existing.UpdatedDate = DateTime.Now;
                    existing.Region = q.Region;
                    existing.Continent = q.Continent;
                    existing.CountryCode = q.CountryCode;
                    existing.DevelopmentCategory = q.DevelopmentCategory;
                    if (!string.IsNullOrEmpty(image))
                    {
                        existing.Image = image;
                    }                    
                    existing.Latitude = q.Latitude;
                    existing.Longitude = q.Longitude;
                    existing.Income = q.Income;
                    existing.Population = q.Population;
                    existing.CountryAliasName = q.CountryAliasName;                    
                    _context.Countries.Update(existing);
                    await _context.SaveChangesAsync();
                    await UpdatePeerCountries(existing.CountryID, q.PeerCountries ?? new List<int>());

                    return ResultResponseDto<string>.Success("", new string[] { "Country edited Successfully" });
                }
                else
                {
                    var payload = new BulkAddCountryDto { Countries = new() { q } };
                    var response = await AddBulkCountryAsync(payload, image);
                    return response;
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in UnAssignCountry", ex);
                return ResultResponseDto<string>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task UpdatePeerCountries(int countryId, List<int> peerCountryIds)
        {
            if (peerCountryIds == null)
                peerCountryIds = new List<int>();

            // Remove self and duplicates
            peerCountryIds = peerCountryIds
                .Where(x => x != countryId)
                .Distinct()
                .ToList();

            var existingPeers = await _context.CountryPeers
                .Where(x => x.CountryID == countryId && !x.IsDeleted)
                .ToListAsync();

            var existingPeerIds = existingPeers
                .Select(x => x.PeerCountryID)
                .ToList();

            // Soft delete removed peers
            var removePeers = existingPeers
                .Where(x => !peerCountryIds.Contains(x.PeerCountryID))
                .ToList();

            foreach (var peer in removePeers)
            {
                peer.IsDeleted = true;
                peer.IsActive = false;
                peer.UpdatedDate = DateTime.UtcNow;
            }

            // Add new peers
            var newPeers = peerCountryIds
                .Where(x => !existingPeerIds.Contains(x))
                .ToList();

            foreach (var peerId in newPeers)
            {
                await _context.CountryPeers.AddAsync(new CountryPeer
                {
                    CountryID = countryId,
                    PeerCountryID = peerId,
                    IsActive = true,
                    UpdatedDate = DateTime.UtcNow
                });
            }
            if (newPeers.Count > 0 || removePeers.Count > 0)
            {
                _download.InsertAnalyticalLayerResults(countryId);
            }

            await _context.SaveChangesAsync();
        }
        public async Task<ResultResponseDto<string>> AddBulkCountryAsync(BulkAddCountryDto request, string image = "")
        {
            try
            {
                // ✅ Normalize input
                var inputCountries = request.Countries
                    .Select(c => new
                    {                        
                        CountryCode = c.CountryCode,
                        CountryName = c.CountryName.Trim().ToLower(),
                        Continent = c.Continent.Trim().ToLower(),
                        Region = c.Region?.Trim(),
                        Longitude = c.Longitude,
                        Latitude = c.Latitude,
                        Income = c.Income,
                        Population = c.Population,
                        CountryAliasName = c.CountryAliasName,
                        DevelopmentCategory = c.DevelopmentCategory,
                        PeerCountries = c.PeerCountries,

                    })
                    .GroupBy(c => new { c.CountryName, c.Continent })
                    .Select(g => g.First())
                    .ToList();

                // ✅ Get existing countries (correct matching)
                var existingCountries = await _context.Countries
                    .Where(x => x.IsActive && !x.IsDeleted)
                    .Select(x => new
                    {
                        CountryName = x.CountryName.ToLower(),
                        State = x.Continent.ToLower()
                    })
                    .ToListAsync();

                var existingSet = new HashSet<string>(
                    existingCountries.Select(x => $"{x.CountryName}_{x.State}")
                );

                // ✅ Filter new countries
                var newCountries = inputCountries
                    .Where(c => !existingSet.Contains($"{c.CountryName}_{c.Continent}"))
                    .ToList();

                var existingCountryNames = inputCountries
                    .Where(c => existingSet.Contains($"{c.CountryName}_{c.Continent}"))
                    .Select(c => $"{c.CountryName}, {c.Continent}")
                    .ToList();

                // ✅ Create country entities
                var countryEntities = newCountries.Select(countryDto => new Country
                {

                    CountryName = countryDto.CountryName,
                    Continent = countryDto.Continent,
                    Region = countryDto.Region,
                    CreatedDate = DateTime.UtcNow,
                    CountryCode = countryDto.CountryCode,
                    IsActive = true,
                    IsDeleted = false,
                    Image = image,
                    Longitude = countryDto.Longitude,
                    Latitude = countryDto.Latitude,
                    Income = countryDto.Income,                    
                    Population = countryDto.Population,
                    CountryAliasName = countryDto.CountryAliasName,
                    DevelopmentCategory = countryDto.DevelopmentCategory
                }).ToList();

                await _context.Countries.AddRangeAsync(countryEntities);
                await _context.SaveChangesAsync();

                var countryPeers = new List<CountryPeer>();

                for (int i = 0; i < newCountries.Count; i++)
                {
                    var dto = newCountries[i];
                    var country = countryEntities[i];

                    if (dto.PeerCountries != null && dto.PeerCountries.Any())
                    {
                        countryPeers.AddRange(dto.PeerCountries.Select(peerId => new CountryPeer
                        {
                            CountryID = country.CountryID,
                            PeerCountryID = peerId,
                            IsActive = true,
                            UpdatedDate = DateTime.UtcNow
                        }));
                    }
                }

                if (countryPeers.Any())
                {
                    await _context.CountryPeers.AddRangeAsync(countryPeers);
                    await _context.SaveChangesAsync();
                }

                // ✅ Response handling
                if (existingCountryNames.Any() && newCountries.Any())
                {
                    return ResultResponseDto<string>.Success(
                        "",
                        new[] { $"{string.Join(", ", existingCountryNames)} already exist" }
                    );
                }
                else if (existingCountryNames.Any())
                {
                    return ResultResponseDto<string>.Failure(
                        new[] { $"{string.Join(", ", existingCountryNames)} already exist" }
                    );
                }
                else
                {
                    return ResultResponseDto<string>.Success(
                        "",
                        new[] { "Country added successfully" }
                    );
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in AddBulkCountryAsync", ex);
                return ResultResponseDto<string>.Failure(new[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<bool>> DeleteCountryAsync(int id)
        {
            try
            {
                var q = await _context.Countries.FindAsync(id);
                if (q == null) return ResultResponseDto<bool>.Failure(new string[] { "Country not exists" });

                q.IsActive = false;
                q.IsDeleted = true;
                q.UpdatedDate = DateTime.UtcNow;
                _context.Countries.Update(q);

                await _context.CountryPeers.Where(x => x.CountryID == id).ForEachAsync(x =>
                {
                    x.IsActive = false;
                    x.IsDeleted = true;
                    x.UpdatedDate = DateTime.UtcNow;
                });

                await _context.CountryPeers.Where(x => x.PeerCountryID == id).ForEachAsync(x =>
                {
                    x.IsActive = false;
                    x.IsDeleted = true;
                    x.UpdatedDate = DateTime.UtcNow;
                });

                await _context.UserCountryMappings.Where(x => x.CountryID == id).ForEachAsync(x =>
                {
                    x.IsDeleted = true;
                });

                await _context.SaveChangesAsync();
                return ResultResponseDto<bool>.Success(true, new string[] { "Country deleted Successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in DeleteCityAsync", ex);
                return ResultResponseDto<bool>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<Country>> EditCountryAsync(int id, AddUpdateCountryDto q)
        {

            try
            {
                var existCity = await _context.Countries.FirstOrDefaultAsync(x => x.IsActive && !x.IsDeleted && q.CountryName == x.CountryName && x.Continent == q.Continent && x.CountryID != id);
                if (existCity != null)
                {
                    return ResultResponseDto<Country>.Failure(new string[] { "Country already exists" });
                }
                var existing = await _context.Countries.FindAsync(id);
                if (existing == null) return ResultResponseDto<Country>.Failure(new string[] { "Country not exists" });
                existing.CountryName = q.CountryName;
                existing.UpdatedDate = DateTime.Now;
                existing.Region = q.Region;
                existing.Continent = q.Continent;
                existing.CountryAliasName = q.CountryAliasName;
                existing.DevelopmentCategory = q.DevelopmentCategory;                
                _context.Countries.Update(existing);
                await _context.SaveChangesAsync();

                return ResultResponseDto<Country>.Success(existing, new string[] { "Country edited Successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in EditCountryAsync", ex);
                return ResultResponseDto<Country>.Failure(new string[] { "There is an error please try later" });
            }
        }

        #region GetCountriesAsync
        public async Task<PaginationResponse<UserCountryMappingResponseDto>> GetCountriesAsync(PaginationRequest request, UserRole role)
        {
            try
            {
                int year = DateTime.UtcNow.Year;

                IQueryable<UserCountryMappingResponseDto> query = role == UserRole.Admin
                    ? GetAdminCountryQuery(year)
                    : GetUserCountryQuery(request.UserId, year);

                // 🔍 Search
                if (!string.IsNullOrWhiteSpace(request.SearchText))
                {
                    string search = request.SearchText.Trim();
                    query = query.Where(x =>
                        x.CountryName.Contains(search) ||
                        x.Continent.Contains(search));
                }

                // 📄 Pagination (DB level)
                var response = await query.ApplyPaginationAsync(request);

                // 🧮 Manual Score Calculation (Non-Country User)
                if (role != UserRole.CountryUser && response.Data.Any())
                {
                    await ApplyManualScoresAsync(response, request, role, year);
                }

                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in GetCountriesAsync", ex);
                return new PaginationResponse<UserCountryMappingResponseDto>();
            }
        }
        private IQueryable<UserCountryMappingResponseDto> GetAdminCountryQuery(int year)
        {
            return
                from c in _context.Countries.AsNoTracking()
                where !c.IsDeleted
                join ai in _context.AICountryScores
                        .Where(x => x.IsVerified && x.Year == year)
                    on c.CountryID equals ai.CountryID into aiJoin
                from ai in aiJoin.DefaultIfEmpty()
                select new UserCountryMappingResponseDto
                {
                    CountryID = c.CountryID,
                    CountryName = c.CountryName,
                    Continent = c.Continent,
                    CountryCode = c.CountryCode,
                    Region = c.Region,                   
                    Image = c.Image,
                    Latitude = c.Latitude,
                    Longitude = c.Longitude,
                    IsActive = c.IsActive,
                    CreatedDate = c.CreatedDate,
                    UpdatedDate = c.UpdatedDate,
                    IsDeleted = c.IsDeleted,
                    Score = 0,
                    AiScore = ai != null ? ai.AIScore : 0,
                    CountryPeers = c.CountryPeers,
                    Population = c.Population,
                    Income = c.Income,                    
                    CountryAliasName = c.CountryAliasName,
                    DevelopmentCategory = c.DevelopmentCategory
                };
        }

        private IQueryable<UserCountryMappingResponseDto> GetUserCountryQuery(long? userId, int? year)
        {
            year = year ?? DateTime.Now.Year;

            return
                from c in _context.Countries.AsNoTracking()
                join cm in _context.UserCountryMappings
                        .Where(x => !x.IsDeleted && x.UserID == userId)
                    on c.CountryID equals cm.CountryID
                join u in _context.Users
                    on cm.AssignedByUserId equals u.UserID
                join ai in _context.AICountryScores
                .Where(x => x.IsVerified && x.Year == year)
            on c.CountryID equals ai.CountryID into aiJoin
                from ai in aiJoin.DefaultIfEmpty()

                where !c.IsDeleted
                select new UserCountryMappingResponseDto
                {
                    CountryID = c.CountryID,
                    Continent = c.Continent,
                    CountryName = c.CountryName,                    
                    CountryCode = c.CountryCode,
                    Region = c.Region,
                    IsActive = c.IsActive,
                    CreatedDate = c.CreatedDate,
                    UpdatedDate = c.UpdatedDate,
                    IsDeleted = c.IsDeleted,
                    AssignedBy = u.FullName,
                    UserCountryMappingID = cm.UserCountryMappingID,
                    Score = 0,
                    AiScore = ai.AIProgress,
                    CountryAliasName = c.CountryAliasName,
                    CountryPeers = c.CountryPeers,
                    Population = c.Population,
                    Income = c.Income,
                    DevelopmentCategory = c.DevelopmentCategory
                };
        }
        private async Task ApplyManualScoresAsync(PaginationResponse<UserCountryMappingResponseDto> response,PaginationRequest request, UserRole role, int year)
        {
            var scores = await _commonService.GetCountriesProgressAsync(request.UserId.GetValueOrDefault(),(int)role, year);
            int pillarCount = _appSettings.PillarCount;
            var scoreMap = scores
                .GroupBy(x => x.CountryID)
                .ToDictionary(
                    g => g.Key,
                    g => Math.Round((g.Sum(x => (decimal?)x.ScoreProgress) ?? 0) / pillarCount, 2));

            foreach (var country in response.Data)
            {
                country.PeerCountryIDs = country.CountryPeers?.Where(x => x.IsActive && !x.IsDeleted).Select(x => x.PeerCountryID).ToList() ?? new List<int>();
                if (scoreMap.TryGetValue(country.CountryID, out var score))
                {
                    country.Score = score;
                }
            }

            // ✅ Correct dynamic sorting
            response.Data = request.SortDirection?.ToLower() == "desc"
                ? response.Data.OrderByDescending(x => x.Score)
                : response.Data.OrderBy(x => x.Score);
        }

        #endregion
        public async Task<ResultResponseDto<List<UserCountryMappingResponseDto>>> getAllCountryByUserId(int userId, UserRole userRole)
        {
            try
            {

                IQueryable<UserCountryMappingResponseDto> countryQuery;

                int year = DateTime.UtcNow.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);
                int pillarCount = _appSettings.PillarCount;
                if (userRole == UserRole.Admin)
                {
                    countryQuery = GetAdminCountryQuery(year);
                }
                else
                {
                    countryQuery = GetUserCountryQuery(userId, year);
                }
                var result = await countryQuery.ToListAsync();

                if (userRole != UserRole.CountryUser)
                {
                    var scores = await _commonService.GetCountriesProgressAsync(userId, (int)userRole, year);

                    var scoreMap = scores
              .GroupBy(x => x.CountryID)
              .ToDictionary(
                  g => g.Key,
                  g => Math.Round((g.Sum(x => (decimal?)x.ScoreProgress) ?? 0) / pillarCount, 2));

                    foreach (var country in result)
                    {
                        if (scoreMap.TryGetValue(country.CountryID, out var score))
                        {
                            country.Score = score;
                        }
                    }

                }
                result = (userRole == UserRole.CountryUser ? result.OrderByDescending(x => x.AiScore) : result.OrderByDescending(x => x.Score)).ToList();

                return ResultResponseDto<List<UserCountryMappingResponseDto>>.Success(result, new string[] { "get successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in getAllCityByUserId", ex);
                return ResultResponseDto<List<UserCountryMappingResponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<Country>> GetByIdAsync(int id)
        {
            try
            {
                var d = await _context.Countries.FirstAsync(x => x.CountryID == id);
                return await Task.FromResult(ResultResponseDto<Country>.Success(d, new string[] { "get successfully" }));
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetByIdAsync", ex);
                return ResultResponseDto<Country>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<object>> AssingCountryToUser(int userId, int countryId, int assignedByUserId)
        {
            try
            {
                if (_context.UserCountryMappings.Any(x => x.UserID == userId && x.CountryID == countryId && x.AssignedByUserId == assignedByUserId && !x.IsDeleted))
                {
                    return await Task.FromResult(ResultResponseDto<object>.Failure(new string[] { "Country already assigned to user" }));
                }
                var user = _context.Users.Find(userId);

                if (user == null)
                {
                    return await Task.FromResult(ResultResponseDto<object>.Failure(new string[] { "Invalid request data." }));
                }
                var mapping = new UserCountryMapping
                {
                    UserID = userId,
                    CountryID = countryId,
                    AssignedByUserId = assignedByUserId,
                    Role = user.Role
                };
                _context.UserCountryMappings.Add(mapping);

                await _context.SaveChangesAsync();

                return await Task.FromResult(ResultResponseDto<object>.Success(new { }, new string[] { "Country assigned successfully" }));
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in AssingCityToUser", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<object>> EditAssingCountry(int id, int userId, int cityId, int assignedByUserId)
        {
            try
            {

                if (_context.UserCountryMappings.Any(x => x.UserID == userId && x.CountryID == cityId && x.AssignedByUserId == assignedByUserId))
                {
                    return ResultResponseDto<object>.Failure(new string[] { "Country already assigned to user" });
                }
                var userMapping = _context.UserCountryMappings.Find(id);

                if (userMapping == null)
                {
                    return ResultResponseDto<object>.Failure(new string[] { "Invalid request data." });
                }

                userMapping.UserID = userId;
                userMapping.CountryID = cityId;
                userMapping.AssignedByUserId = assignedByUserId;
                _context.UserCountryMappings.Update(userMapping);
                await _context.SaveChangesAsync();

                return ResultResponseDto<object>.Success(new { }, new string[] { "Assigned country updated successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<object>> UnAssignCountry(UserCountryUnMappingRequestDto requestDto)
        {
            try
            {
                var userMapping = _context.UserCountryMappings.Where(x => x.UserID == requestDto.UserId && x.AssignedByUserId == requestDto.AssignedByUserId && !x.IsDeleted).ToList();
                if (userMapping == null && userMapping?.Count == 0)
                {
                    return await Task.FromResult(ResultResponseDto<object>.Failure(new string[] { "user has no assign country" }));
                }
                foreach (var m in userMapping)
                {
                    m.IsDeleted = true;
                    _context.UserCountryMappings.Update(m);
                }

                await _context.SaveChangesAsync();

                return ResultResponseDto<object>.Success(new { }, new string[] { "Assigned country deleted successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in UnAssignCity", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<List<UserCountryMappingResponseDto>>> GetCountryByUserIdForAssessment(int userId)
        {
            try
            {

                var user = await _context.Users.FirstOrDefaultAsync(x => x.UserID == userId);

                if (user == null)
                {
                    return ResultResponseDto<List<UserCountryMappingResponseDto>>.Failure(new string[] { "Invalid user" });
                }
                var year = DateTime.Now.Year;
                Expression<Func<Assessment, bool>>  predicate = a => 
                !a.UserCountryMapping.IsDeleted 
                && a.UserCountryMapping.UserID == userId 
                && a.UpdatedAt.Year == year
                && (a.AssessmentPhase == AssessmentPhase.Completed || a.AssessmentPhase == AssessmentPhase.EditRejected || a.AssessmentPhase == AssessmentPhase.EditRequested);

                // Get distinct UserCityMappings which are not show to user
                var userCityMappingIds = _context.Assessments
                    .Where(predicate)
                    .Select(a => a.UserCountryMappingID)
                    .Distinct();

                // Project into response DTO
                var cityQuery =
                     from c in _context.Countries
                     join cm in _context.UserCountryMappings
                         .Where(x => !x.IsDeleted && x.UserID == userId && !userCityMappingIds.Contains(x.UserCountryMappingID))
                         on c.CountryID equals cm.CountryID
                     join u in _context.Users on cm.AssignedByUserId equals u.UserID
                     select new UserCountryMappingResponseDto
                     {
                         CountryID = c.CountryID,
                         Continent = c.Continent,
                         CountryName = c.CountryName,
                         CountryCode = c.CountryCode,
                         Region = c.Region,
                         IsActive = c.IsActive,
                         CreatedDate = c.CreatedDate,
                         UpdatedDate = c.UpdatedDate,
                         IsDeleted = c.IsDeleted,
                         AssignedBy = u.FullName,
                         UserCountryMappingID = cm.UserCountryMappingID
                     };

                var result = await cityQuery.ToListAsync();

                if (!result.Any())
                {
                    return ResultResponseDto<List<UserCountryMappingResponseDto>>.Failure(new string[] { "No country is found for assessment" });
                }

                return ResultResponseDto<List<UserCountryMappingResponseDto>>.Success(result, new string[] { "Retrieved successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetCityByUserIdForAssessment", ex);
                return ResultResponseDto<List<UserCountryMappingResponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<CountryHistoryDto>> GetCountryHistory(int userID, DateTime updatedAt, UserRole userRole)
        {
            try
            {
                var year = updatedAt.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);

                var cityHistory = new CountryHistoryDto();

                Expression<Func<UserCountryMapping, bool>> predicate;

                if (userRole == UserRole.Analyst)
                    predicate = x => !x.IsDeleted && (x.AssignedByUserId == userID || x.UserID == userID);
                else if(userRole == UserRole.Evaluator)
                    predicate = x => !x.IsDeleted && x.UserID == userID;
                else
                    predicate = x => !x.IsDeleted;

                // 1️⃣ Get country-related counts in a single round trip
                var cityQuery = await (
                    from c in _context.Countries
                    where !c.IsDeleted && c.IsActive
                    join uc in _context.UserCountryMappings.Where(predicate)
                        on c.CountryID equals uc.CountryID 
                    join a in _context.Assessments.Where(x => x.IsActive && x.UpdatedAt >= startDate && x.UpdatedAt <= endDate)
                        on uc.UserCountryMappingID equals a.UserCountryMappingID into cityAssessments 
                    from a in cityAssessments.DefaultIfEmpty()
                    select new
                    {
                        c.CountryID,
                        HasMapping = uc != null,
                        IsCompleted = a != null && a.AssessmentPhase == AssessmentPhase.Completed
                    }
                ).ToListAsync();

                // First, extract the list of CityIDs from your cityQuery
                var cityIds = cityQuery.Select(c => c.CountryID).Distinct().ToList();

                // Then, get all AICityScores for those cities
                var aICity = await _context.AICountryScores
                    .Where(x => cityIds.Contains(x.CountryID) && x.Year == year)
                    .ToListAsync();

                cityHistory.TotalCountry = cityQuery.Select(x => x.CountryID).Distinct().Count();
                cityHistory.ActiveCountry = cityQuery.Where(x => x.HasMapping).Select(x => x.CountryID).Distinct().Count();
                cityHistory.CompeleteCountry = cityQuery.Where(x => x.IsCompleted).Select(x => x.CountryID).Distinct().Count();
                cityHistory.InprocessCountry = cityHistory.ActiveCountry - cityHistory.CompeleteCountry;
                cityHistory.FinalizeCountry = aICity.Where(x=>x.IsVerified).Count();
                cityHistory.UnFinalize = aICity.Where(x => !x.IsVerified).Count();

                // 2️⃣ Get evaluators & analysts in a single query
                var userCounts = await _context.Users
                    .Where(u => !u.IsDeleted && (u.Role == UserRole.Evaluator || u.Role == UserRole.Analyst))
                    .GroupBy(u => u.Role)
                    .Select(g => new { Role = g.Key, Count = g.Count() })
                    .ToListAsync();
                if(userRole == UserRole.Admin)
                {
                    cityHistory.TotalEvaluator = userCounts.FirstOrDefault(x => x.Role == UserRole.Evaluator)?.Count ?? 0;
                    cityHistory.TotalAnalyst = userCounts.FirstOrDefault(x => x.Role == UserRole.Analyst)?.Count ?? 0;
                }

                return ResultResponseDto<CountryHistoryDto>.Success(cityHistory, new List<string> { "Get history successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetCityHistory", ex);
                return ResultResponseDto<CountryHistoryDto>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<List<GetCountriesSubmitionHistoryResponseDto>>> GetCountriesProgressByUserId(int userID, DateTime updatedAt, UserRole userRole)
        {
            try
            {
                int year = updatedAt.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);

                // Get total pillars and questions (independent query)
                var pillarStats = await _context.Pillars
                    .Select(p => new { QuestionsCount = p.Questions.Count() })
                    .ToListAsync();

                int totalPillars = pillarStats.Count;
                int totalQuestions = pillarStats.Sum(p => p.QuestionsCount);

                Expression<Func<UserCountryMapping, bool>> predicate;

                if (userRole == UserRole.Analyst)
                    predicate = x => !x.IsDeleted && (x.AssignedByUserId == userID || x.UserID == userID);
                else
                    predicate = x => !x.IsDeleted && x.UserID == userID;


                var cityRaw = await (
                    from uc in _context.UserCountryMappings.Where(predicate)
                    join c in _context.Countries.Where(c => !c.IsDeleted && c.IsActive)
                        on uc.CountryID equals c.CountryID
                    join a in _context.AICountryScores.Where(x =>  x.Year == year)
                        on c.CountryID equals a.CountryID into aICityScores
                    from a in aICityScores.DefaultIfEmpty()
                    select new
                    {
                        c.CountryID,
                        c.CountryName,
                        a.AIProgress
                    }
                ).AsNoTracking().Distinct().ToListAsync();  // 🚀 force materialization first


                var manualAssessmentList = await _commonService.GetCountriesProgressAsync(userID, (int)userRole, year);

                // Now do grouping/aggregation in memory (LINQ to Objects)
                var citySubmission = cityRaw
                    .Select(g =>
                    {
                        var allPillars = manualAssessmentList.Where(x => x.CountryID == g.CountryID);

                        var manualScore = allPillars.Any()? allPillars.Average(x => x?.ScoreProgress ?? 0): 0;

                        return new GetCountriesSubmitionHistoryResponseDto
                        {
                            CountryID = g.CountryID,
                            CountryName = g.CountryName,
                            Score = Math.Round(manualScore,2),
                            AnsQuestion = allPillars.Any() ? allPillars.Sum(x=>x?.TotalAns ?? 0) : 0,
                            ScoreProgress = Math.Round(manualScore, 2),
                            AIScore = g.AIProgress ?? 0
                        };
                    }).ToList();

                return ResultResponseDto<List<GetCountriesSubmitionHistoryResponseDto>>.Success(citySubmission ?? new(), new List<string> { "Get Cities history successfully" });

            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetCitiesProgressByUserId", ex);
                return ResultResponseDto<List<GetCountriesSubmitionHistoryResponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<List<UserCountryMappingResponseDto>>> getAllCountryByLocation(GetNearestCountryRequestDto r)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(x => x.UserID == r.UserID);
                if (user == null)
                    return ResultResponseDto<List<UserCountryMappingResponseDto>>.Failure(new[] { "Invalid user" });

                var year = DateTime.Now.Year;

                Expression<Func<Assessment, bool>> predicate = a =>
                    !a.UserCountryMapping.IsDeleted &&
                    a.UserCountryMapping.UserID == r.UserID &&
                    a.UpdatedAt.Year == year &&
                    (a.AssessmentPhase == AssessmentPhase.Completed ||
                     a.AssessmentPhase == AssessmentPhase.EditRejected ||
                     a.AssessmentPhase == AssessmentPhase.EditRequested);

                var userCityMappingIds = await _context.Assessments
                    .Where(predicate)
                    .Select(a => a.UserCountryMappingID)
                    .Distinct()
                    .ToListAsync();

                // First get data from DB (no static method call inside query)
                var cityList = await (
                    from c in _context.Countries
                    join cm in _context.UserCountryMappings
                        .Where(x => !x.IsDeleted && x.UserID == r.UserID && !userCityMappingIds.Contains(x.UserCountryMappingID))
                        on c.CountryID equals cm.CountryID
                    join u in _context.Users on cm.AssignedByUserId equals u.UserID
                    select new UserCountryMappingResponseDto
                    {
                        CountryID = c.CountryID,
                        Continent = c.Continent,
                        CountryName = c.CountryName,
                        CountryCode = c.CountryCode,
                        Region = c.Region,
                        IsActive = c.IsActive,
                        CreatedDate = c.CreatedDate,
                        UpdatedDate = c.UpdatedDate,
                        IsDeleted = c.IsDeleted,
                        AssignedBy = u.FullName,
                        UserCountryMappingID = cm.UserCountryMappingID,
                        Latitude = c.Latitude,
                        Longitude = c.Longitude
                    }).ToListAsync();

                // Then calculate distance in memory using static method
                foreach (var country in cityList)
                {
                    if (country.Latitude.HasValue && country.Longitude.HasValue)
                        country.Distance = HaversineDistance(r.Latitude, r.Longitude, country.Latitude.Value, country.Longitude.Value);
                    else
                        country.Distance = double.MaxValue;
                }

                var result = cityList.OrderBy(x => x.Distance).ToList();

                if (!result.Any())
                    return ResultResponseDto<List<UserCountryMappingResponseDto>>.Failure(new[] { "No country is found for assessment" });

                return ResultResponseDto<List<UserCountryMappingResponseDto>>.Success(result, new[] { "Retrieved successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetAllCityByLocation", ex);
                return ResultResponseDto<List<UserCountryMappingResponseDto>>.Failure(new[] { "An error occurred, please try later" });
            }
        }

        private double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371; // Radius of Earth in km
            var dLat = (lat2 - lat1) * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;

            lat1 = lat1 * Math.PI / 180.0;
            lat2 = lat2 * Math.PI / 180.0;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);
            var c = 2 * Math.Asin(Math.Sqrt(a));
            return R * c;
        }
        
        public async Task<ResultResponseDto<List<UserCountryMappingResponseDto>>> GetAiAccessCountry(int userId, UserRole userRole)
        {
            try
            {
                IQueryable<UserCountryMappingResponseDto> countryQuery;
                int year = DateTime.UtcNow.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);

                // Step 1️⃣: Fetch country score averages as a dictionary
                var countryScoresQuery =
                   from ar in _context.AICountryScores
                   where ar.UpdatedAt >= startDate && ar.UpdatedAt < endDate
                   group ar by ar.CountryID into g
                   select new
                   {
                       CountryID = g.Key,
                       Score = g.Average(x => (decimal?)x.AIProgress) ?? 0
                   };

                countryQuery =
                    from c in _context.Countries
                    join cm in _context.AIUserCountryMappings
                        .Where(x => x.IsActive && x.UserID == userId)
                        on c.CountryID equals cm.CountryID
                    join u in _context.Users on cm.AssignBy equals u.UserID
                    join cs in countryScoresQuery on cm.CountryID equals cs.CountryID into scoreGroup
                    from cs in scoreGroup.DefaultIfEmpty()
                    select new UserCountryMappingResponseDto
                    {
                        CountryID = c.CountryID,
                        Continent = c.Continent,
                        CountryName = c.CountryName,
                        CountryCode = c.CountryCode,
                        Region = c.Region,
                        IsActive = c.IsActive,
                        CreatedDate = c.CreatedDate,
                        UpdatedDate = c.UpdatedDate,
                        IsDeleted = c.IsDeleted,
                        AssignedBy = u.FullName,
                        UserCountryMappingID = cm.AIUserCountryMappingID,
                        Score = cs.Score,
                    };
                var result = await countryQuery
                    .OrderByDescending(x => x.Score)
                                .ToListAsync();

                return ResultResponseDto<List<UserCountryMappingResponseDto>>.Success(result, new string[] { "get successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in getAllCityByUserId", ex);
                return ResultResponseDto<List<UserCountryMappingResponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }       
        public async Task<ResultResponseDto<byte[]>> ExportCountries(ExportCountryWithOptionDto request, int userId, UserRole userRole)
        {
            try
            {
                int year = DateTime.UtcNow.Year;
                var countries = await _commonService.GetCountriesProgressForAdmin(userId, (int)userRole, year);

                if (countries == null) return ResultResponseDto<byte[]>.Failure(new string[] { "There is an error please try later" });
                IEnumerable<IGrouping<(int CountryID, string CountryName, string Continent), GetCountriesProgressAdminDto>>
                result =
                    countries.Where(x => request.CountryIDs==null  || request.CountryIDs.Count==0 || request.CountryIDs.Contains(x.CountryID) || request.IsAllCountry==true)
                    .GroupBy(x => (
                        x.CountryID,
                        x.CountryName,
                        x.Continent                       
                    )).OrderByDescending(x => x.Average(y => y.PillarProgress));

                var byteRes = MakeCountryPillarSheet(request, result);

                return ResultResponseDto<byte[]>.Success(byteRes, new string[] { "get successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in getAllCityByUserId", ex);
                return ResultResponseDto<byte[]>.Failure(new string[] { "There is an error please try later" });
            }
        }
        private byte[] MakeCountryPillarSheet(
            ExportCountryWithOptionDto request,
            IEnumerable<IGrouping<(int CountryID, string CountryName, string Continent), GetCountriesProgressAdminDto>> cityGroups)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Countries Progress Report");

            bool isRanking = request.IsRanking == true;

            // ---------------- Header ----------------
            int totalColumns = isRanking ? 5 : 10;

            ws.Range(1, 1, 1, totalColumns).Merge().Value = "Countries Progress Report";
            ws.Range(2, 1, 2, totalColumns).Merge().Value = $"Report Year: {DateTime.UtcNow.Year}";
            ws.Range(3, 1, 3, totalColumns).Merge().Value = $"Generated On: {DateTime.UtcNow:dd-MMM-yyyy HH:mm}";

            var headerRange = ws.Range(1, 1, 3, totalColumns);
            headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(57, 123, 103);
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            int row = 5;

            // ---------------- Column Header ----------------
            ws.Cell(row, 1).Value = "S.No.";
            ws.Cell(row, 2).Value = "Country Name";
            ws.Cell(row, 3).Value = "Continent";           

            if (isRanking)
            {
                ws.Cell(row, 5).Value = "Evaluator - AI Country Progress (%)";
            }
            else
            {
                ws.Cell(row, 5).Value = "Pillar Name";
                ws.Cell(row, 6).Value = "Total Score";
                ws.Cell(row, 7).Value = "Total Answers";
                ws.Cell(row, 8).Value = "Evaluator Pillar Progress (%)";
                ws.Cell(row, 9).Value = "AI Pillar Progress (%)";
                ws.Cell(row, 10).Value = "Evaluator - AI Country Progress (%)";
            }

            var header = ws.Range(row, 1, row, totalColumns);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromArgb(57, 123, 103);
            header.Style.Font.FontColor = XLColor.White;

            row++;
            int sno = 1;

            // ---------------- Data ----------------
            foreach (var cityGroup in cityGroups)
            {
                var cityData = cityGroup.First();
                var pillars = cityGroup.OrderBy(x => x.DisplayOrder).ToList();

                var cityProgress = pillars.Average(x => x.PillarProgress);

                // ========================
                // ✅ RANK-WISE (1 ROW ONLY)
                // ========================
                if (isRanking)
                {
                    ws.Cell(row, 1).Value = sno++;
                    ws.Cell(row, 2).Value = cityData.CountryName;
                    ws.Cell(row, 3).Value = cityData.Continent;                    
                    ws.Cell(row, 5).Value = $"{cityProgress:F2}% - {cityData.AICountryProgress:F2}%";

                    row++;
                    continue;
                }

                // ========================
                // ✅ PILLAR-WISE
                // ========================
                int startRow = row;
                bool first = true;

                foreach (var pillar in pillars)
                {
                    ws.Cell(row, 1).Value = sno++;
                    ws.Cell(row, 2).Value = cityData.CountryName;
                    ws.Cell(row, 3).Value = cityData.Continent;                    

                    ws.Cell(row, 5).Value = pillar.PillarName;
                    ws.Cell(row, 6).Value = pillar.TotalScore;
                    ws.Cell(row, 7).Value = pillar.TotalAns;
                    ws.Cell(row, 8).Value = $"{pillar.PillarProgress:F2}%";
                    ws.Cell(row, 9).Value = $"{pillar.AIPillarProgress:F2}%";

                    if (first)
                    {
                        ws.Cell(row, 10).Value = $"{cityProgress:F2}% - {cityData.AICountryProgress:F2}%";
                        first = false;
                    }

                    row++;
                }

                int endRow = row - 1;

                // Merge only for pillar mode
                if (endRow > startRow)
                {
                    ws.Range(startRow, 2, endRow, 2).Merge();
                    ws.Range(startRow, 3, endRow, 3).Merge();
                    ws.Range(startRow, 4, endRow, 4).Merge();
                    ws.Range(startRow, 10, endRow, 10).Merge();
                }
            }

            // ---------------- Formatting ----------------
            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(5);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
        #endregion
    }
}
