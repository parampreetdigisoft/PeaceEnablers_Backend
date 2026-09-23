namespace PeaceEnablers.Dtos.AiDto
{
    public class AiCurrentJobsFilterDto
    {
        /// <summary>When true, only queued/running jobs. When false, all in-memory jobs.</summary>
        public bool InflightOnly { get; set; } = true;

        /// <summary>Filter by user role, e.g. Admin, Analyst, Evaluator, CountryUser.</summary>
        public string? Role { get; set; }

        /// <summary>Search by user full name or email (contains, case-insensitive).</summary>
        public string? UserName { get; set; }

        /// <summary>Search by job name / purpose / resource type / coalescing key.</summary>
        public string? JobName { get; set; }
    }

    public class AiJobsListResponse
    {
        public List<AiJobRawDto> Jobs { get; set; } = new();
    }

    public class AiJobRawDto
    {
        public string JobId { get; set; } = string.Empty;
        public string CoalescingKey { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? CountryId { get; set; }
        public int? PillarId { get; set; }
        public int? QuestionId { get; set; }
        public List<string> AttachedUserIds { get; set; } = new();
        public Dictionary<string, List<string>>? AttachedUserRoles { get; set; }
        public double ElapsedSeconds { get; set; }
        public string? Provider { get; set; }
        public string? Model { get; set; }
        public string? Purpose { get; set; }
        public string? CreatedBy { get; set; }
        public List<string>? CreatedByRoles { get; set; }
        public bool Attached { get; set; }
        public string? Error { get; set; }
        public string? CreatedAt { get; set; }
        public string? StartedAt { get; set; }
        public string? CompletedAt { get; set; }
    }

    public class AiCurrentJobUserDto
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsSystem { get; set; }
    }

    public class AiCurrentJobDto
    {
        public string JobId { get; set; } = string.Empty;
        public string JobName { get; set; } = string.Empty;
        public string CoalescingKey { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? CountryId { get; set; }
        public int? PillarId { get; set; }
        public int? QuestionId { get; set; }
        public double ElapsedSeconds { get; set; }
        public string? Provider { get; set; }
        public string? Model { get; set; }
        public string? Purpose { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public string? CreatedByRole { get; set; }
        public List<AiCurrentJobUserDto> Users { get; set; } = new();
        public string? Error { get; set; }
        public string? CreatedAt { get; set; }
        public string? StartedAt { get; set; }
        public string? CompletedAt { get; set; }
        public bool CanCancel { get; set; }
    }

    public class AiCurrentJobsResultDto
    {
        public List<AiCurrentJobDto> Jobs { get; set; } = new();
        public int TotalCount { get; set; }
        public int InflightCount { get; set; }
    }

    public class AiCurrentJobsHealthDto
    {
        public string Status { get; set; } = string.Empty;
    }

    public class AiCurrentJobsCancelResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public AiJobRawDto? Job { get; set; }
    }
}
