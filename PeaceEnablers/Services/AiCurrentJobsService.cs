using Microsoft.EntityFrameworkCore;
using PeaceEnablers.AI;
using PeaceEnablers.AI.Endpoints;
using PeaceEnablers.Common.Interface;
using PeaceEnablers.Common.Models;
using PeaceEnablers.Data;
using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;

namespace PeaceEnablers.Services
{
    public class AiCurrentJobsService : IAiCurrentJobsService
    {
        private readonly IAiGateway _aiGateway;
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;

        private static readonly HashSet<string> InflightStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "queued", "running"
        };

        public AiCurrentJobsService(
            IAiGateway aiGateway,
            ApplicationDbContext context,
            IAppLogger appLogger)
        {
            _aiGateway = aiGateway;
            _context = context;
            _appLogger = appLogger;
        }

        public async Task<ResultResponseDto<AiCurrentJobsHealthDto>> GetHealthAsync()
        {
            try
            {
                var call = await _aiGateway.SendAsync<AiCurrentJobsHealthDto>(
                    HttpMethod.Get,
                    AdminAiEndpoints.Health);

                if (!call.Success || call.Data == null)
                {
                    return ResultResponseDto<AiCurrentJobsHealthDto>.Failure(
                        new[] { call.Message ?? "Failed to fetch AI health." });
                }

                return ResultResponseDto<AiCurrentJobsHealthDto>.Success(
                    call.Data,
                    new[] { "AI health fetched successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in AiCurrentJobsService.GetHealthAsync", ex);
                return ResultResponseDto<AiCurrentJobsHealthDto>.Failure(
                    new[] { "Failed to fetch AI health." });
            }
        }

        public async Task<ResultResponseDto<AiCurrentJobsResultDto>> GetJobsAsync(AiCurrentJobsFilterDto filter)
        {
            try
            {
                filter ??= new AiCurrentJobsFilterDto();
                var path = filter.InflightOnly ? AdminAiEndpoints.Jobs : AdminAiEndpoints.AllJobs;

                var call = await _aiGateway.SendAsync<AiJobsListResponse>(HttpMethod.Get, path);
                if (!call.Success || call.Data == null)
                {
                    return ResultResponseDto<AiCurrentJobsResultDto>.Failure(
                        new[] { call.Message ?? "Failed to fetch AI jobs." });
                }

                var rawJobs = call.Data.Jobs ?? new List<AiJobRawDto>();
                var enriched = await EnrichJobsAsync(rawJobs);
                var filtered = ApplyFilters(enriched, filter);

                var result = new AiCurrentJobsResultDto
                {
                    Jobs = filtered
                        .OrderByDescending(j => InflightStatuses.Contains(j.Status))
                        .ThenByDescending(j => j.CreatedAt)
                        .ToList(),
                    TotalCount = filtered.Count,
                    InflightCount = enriched.Count(j => InflightStatuses.Contains(j.Status))
                };

                return ResultResponseDto<AiCurrentJobsResultDto>.Success(
                    result,
                    new[] { "AI jobs fetched successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in AiCurrentJobsService.GetJobsAsync", ex);
                return ResultResponseDto<AiCurrentJobsResultDto>.Failure(
                    new[] { "Failed to fetch AI jobs." });
            }
        }

        public async Task<ResultResponseDto<AiCurrentJobDto>> GetJobAsync(string jobId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jobId))
                {
                    return ResultResponseDto<AiCurrentJobDto>.Failure(new[] { "Job id is required." });
                }

                var call = await _aiGateway.SendAsync<AiJobRawDto>(
                    HttpMethod.Get,
                    AdminAiEndpoints.GetJob(jobId));

                if (!call.Success || call.Data == null)
                {
                    return ResultResponseDto<AiCurrentJobDto>.Failure(
                        new[] { call.Message ?? "Job not found." });
                }

                var enriched = (await EnrichJobsAsync(new List<AiJobRawDto> { call.Data })).FirstOrDefault();
                if (enriched == null)
                {
                    return ResultResponseDto<AiCurrentJobDto>.Failure(new[] { "Job not found." });
                }

                return ResultResponseDto<AiCurrentJobDto>.Success(
                    enriched,
                    new[] { "AI job fetched successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in AiCurrentJobsService.GetJobAsync", ex);
                return ResultResponseDto<AiCurrentJobDto>.Failure(
                    new[] { "Failed to fetch AI job." });
            }
        }

        public async Task<ResultResponseDto<object>> GetCountryStatusAsync(int countryId)
        {
            try
            {
                var call = await _aiGateway.SendAsync<object>(
                    HttpMethod.Get,
                    AdminAiEndpoints.CountryStatus(countryId));

                if (!call.Success || call.Data == null)
                {
                    return ResultResponseDto<object>.Failure(
                        new[] { call.Message ?? "Failed to fetch country AI status." });
                }

                return ResultResponseDto<object>.Success(
                    call.Data,
                    new[] { "Country AI status fetched successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in AiCurrentJobsService.GetCountryStatusAsync", ex);
                return ResultResponseDto<object>.Failure(
                    new[] { "Failed to fetch country AI status." });
            }
        }

        public async Task<ResultResponseDto<AiCurrentJobDto>> CancelJobAsync(string jobId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jobId))
                {
                    return ResultResponseDto<AiCurrentJobDto>.Failure(new[] { "Job id is required." });
                }

                var call = await _aiGateway.SendAsync<AiCurrentJobsCancelResponseDto>(
                    HttpMethod.Post,
                    AdminAiEndpoints.CancelJob(jobId));

                if (!call.Success || call.Data?.Job == null)
                {
                    return ResultResponseDto<AiCurrentJobDto>.Failure(
                        new[] { call.Message ?? call.Data?.Message ?? "Failed to cancel job." });
                }

                var enriched = (await EnrichJobsAsync(new List<AiJobRawDto> { call.Data.Job })).First();
                return ResultResponseDto<AiCurrentJobDto>.Success(
                    enriched,
                    new[] { call.Data.Message ?? "Job cancelled." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in AiCurrentJobsService.CancelJobAsync", ex);
                return ResultResponseDto<AiCurrentJobDto>.Failure(
                    new[] { "Failed to cancel AI job." });
            }
        }

        private async Task<List<AiCurrentJobDto>> EnrichJobsAsync(List<AiJobRawDto> rawJobs)
        {
            var idStrings = rawJobs
                .SelectMany(j => (j.AttachedUserIds ?? new List<string>()).Concat(
                    string.IsNullOrWhiteSpace(j.CreatedBy)
                        ? Enumerable.Empty<string>()
                        : new[] { j.CreatedBy }))
                .Where(id => !string.IsNullOrWhiteSpace(id)
                    && !id.Equals("system", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToList();

            var userIds = idStrings
                .Select(id => int.TryParse(id, out var n) ? n : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var users = userIds.Count == 0
                ? new Dictionary<int, User>()
                : await _context.Users
                    .AsNoTracking()
                    .Where(u => userIds.Contains(u.UserID) && !u.IsDeleted)
                    .ToDictionaryAsync(u => u.UserID);

            return rawJobs.Select(raw => MapJob(raw, users)).ToList();
        }

        private static AiCurrentJobDto MapJob(AiJobRawDto raw, Dictionary<int, User> users)
        {
            var jobUsers = new List<AiCurrentJobUserDto>();
            foreach (var uid in raw.AttachedUserIds ?? new List<string>())
            {
                jobUsers.Add(ResolveUser(uid, raw.AttachedUserRoles, users));
            }

            if (!string.IsNullOrWhiteSpace(raw.CreatedBy)
                && !(raw.AttachedUserIds?.Contains(raw.CreatedBy) ?? false))
            {
                jobUsers.Insert(0, ResolveUser(raw.CreatedBy, null, users, raw.CreatedByRoles));
            }

            var created = ResolveUser(raw.CreatedBy ?? "system", null, users, raw.CreatedByRoles);
            var jobName = !string.IsNullOrWhiteSpace(raw.Purpose)
                ? raw.Purpose!
                : !string.IsNullOrWhiteSpace(raw.ResourceType)
                    ? raw.ResourceType
                    : raw.CoalescingKey;

            return new AiCurrentJobDto
            {
                JobId = raw.JobId,
                JobName = jobName,
                CoalescingKey = raw.CoalescingKey,
                ResourceType = raw.ResourceType,
                Status = raw.Status,
                CountryId = raw.CountryId,
                PillarId = raw.PillarId,
                QuestionId = raw.QuestionId,
                ElapsedSeconds = raw.ElapsedSeconds,
                Provider = raw.Provider,
                Model = raw.Model,
                Purpose = raw.Purpose,
                CreatedBy = raw.CreatedBy,
                CreatedByName = created.FullName,
                CreatedByRole = created.Role,
                Users = jobUsers,
                Error = raw.Error,
                CreatedAt = raw.CreatedAt,
                StartedAt = raw.StartedAt,
                CompletedAt = raw.CompletedAt,
                CanCancel = InflightStatuses.Contains(raw.Status)
            };
        }

        private static AiCurrentJobUserDto ResolveUser(
            string userId,
            Dictionary<string, List<string>>? roleMap,
            Dictionary<int, User> users,
            List<string>? fallbackRoles = null)
        {
            if (string.IsNullOrWhiteSpace(userId) || userId.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                var systemRole = fallbackRoles?.FirstOrDefault()
                    ?? roleMap?.GetValueOrDefault("system")?.FirstOrDefault()
                    ?? "Admin";
                return new AiCurrentJobUserDto
                {
                    UserId = "system",
                    FullName = "System (background)",
                    Email = string.Empty,
                    Role = systemRole,
                    IsSystem = true
                };
            }

            var rolesFromJob = roleMap?.GetValueOrDefault(userId) ?? fallbackRoles;
            if (int.TryParse(userId, out var id) && users.TryGetValue(id, out var user))
            {
                return new AiCurrentJobUserDto
                {
                    UserId = userId,
                    FullName = user.FullName ?? $"User {userId}",
                    Email = user.Email ?? string.Empty,
                    Role = user.Role.ToString(),
                    IsSystem = false
                };
            }

            return new AiCurrentJobUserDto
            {
                UserId = userId,
                FullName = $"User {userId}",
                Email = string.Empty,
                Role = rolesFromJob?.FirstOrDefault() ?? "Unknown",
                IsSystem = false
            };
        }

        private static List<AiCurrentJobDto> ApplyFilters(
            List<AiCurrentJobDto> jobs,
            AiCurrentJobsFilterDto filter)
        {
            IEnumerable<AiCurrentJobDto> query = jobs;

            if (!string.IsNullOrWhiteSpace(filter.Role))
            {
                var role = filter.Role.Trim();
                query = query.Where(j =>
                    j.Users.Any(u => u.Role.Equals(role, StringComparison.OrdinalIgnoreCase))
                    || (j.CreatedByRole?.Equals(role, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (!string.IsNullOrWhiteSpace(filter.UserName))
            {
                var name = filter.UserName.Trim();
                query = query.Where(j =>
                    j.Users.Any(u =>
                        (!string.IsNullOrEmpty(u.FullName) && u.FullName.Contains(name, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrEmpty(u.Email) && u.Email.Contains(name, StringComparison.OrdinalIgnoreCase)))
                    || (!string.IsNullOrEmpty(j.CreatedByName)
                        && j.CreatedByName.Contains(name, StringComparison.OrdinalIgnoreCase)));
            }

            if (!string.IsNullOrWhiteSpace(filter.JobName))
            {
                var jobName = filter.JobName.Trim();
                query = query.Where(j =>
                    (!string.IsNullOrEmpty(j.JobName) && j.JobName.Contains(jobName, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrEmpty(j.Purpose) && j.Purpose.Contains(jobName, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrEmpty(j.ResourceType) && j.ResourceType.Contains(jobName, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrEmpty(j.CoalescingKey) && j.CoalescingKey.Contains(jobName, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrEmpty(j.JobId) && j.JobId.Contains(jobName, StringComparison.OrdinalIgnoreCase)));
            }

            return query.ToList();
        }
    }
}
