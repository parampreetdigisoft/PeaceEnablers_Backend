using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Models;

namespace PeaceEnablers.Dtos.AiDto
{
    public class GrantAIEditPermissionDto
    {
        public int UserID { get; set; }
        /// <summary>Up to 5 countries for one analyst/year grant.</summary>
        public List<int> CountryIDs { get; set; }
        public int Year { get; set; }
        public string? Notes { get; set; }
    }

    public class RequestAIEditPermissionDto
    {
        public int CountryID { get; set; }
        public int Year { get; set; }
        public string? Notes { get; set; }
    }

    public class ReviewAIEditPermissionDto
    {
        public int PermissionID { get; set; }
        public bool Approve { get; set; }
        public string? Notes { get; set; }
    }

    public class ReviewAIEditSessionDto
    {
        public int SessionID { get; set; }
        public bool Approve { get; set; }
        public string? ReviewComment { get; set; }
    }

    public class AIEditPermissionListRequestDto : PaginationRequest
    {
        public int? CountryID { get; set; }
        public int? Year { get; set; }
        public int? UserID { get; set; }
        public byte? Status { get; set; }
    }

    public class AIEditSessionListRequestDto : PaginationRequest
    {
        public int? CountryID { get; set; }
        public int? Year { get; set; }
        public byte? Status { get; set; }
    }

    public class AIEditPermissionDto
    {
        public int PermissionID { get; set; }
        public int UserID { get; set; }
        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
        public int CountryID { get; set; }
        public string? CountryName { get; set; }
        public int Year { get; set; }
        public AIEditPermissionStatus Status { get; set; }
        public string StatusName => Status.ToString();
        public DateTime RequestedAt { get; set; }
        public int? GrantedBy { get; set; }
        public string? GrantedByName { get; set; }
        public DateTime? GrantedAt { get; set; }
        public string? Notes { get; set; }
        public int? ActiveSessionID { get; set; }
    }

    public class AIEditSessionDto
    {
        public int SessionID { get; set; }
        public int PermissionID { get; set; }
        public int UserID { get; set; }
        public string? UserName { get; set; }
        public int CountryID { get; set; }
        public string? CountryName { get; set; }
        public int Year { get; set; }
        public AIEditSessionStatus Status { get; set; }
        public string StatusName => Status.ToString();
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public int? ReviewedBy { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewComment { get; set; }
        public int ChangeCount { get; set; }
        public int FieldCount { get; set; }
    }

    public class AIEditChangeCompareDto
    {
        public AIEditEntityType EntityType { get; set; }
        public string EntityTypeName => EntityType.ToString();
        public int EntityRecordID { get; set; }
        public int CountryID { get; set; }
        public int Year { get; set; }
        public int? PillarID { get; set; }
        public string? PillarName { get; set; }
        public int? QuestionID { get; set; }
        public string? QuestionText { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string? BaselineValue { get; set; }
        public string? ProposedValue { get; set; }
        public int EditCount { get; set; }
        public DateTime FirstChangedAt { get; set; }
        public DateTime LastChangedAt { get; set; }
        public List<AIEditChangeLogDto> Trail { get; set; } = new();
    }

    public class AIEditChangeLogDto
    {
        public long ChangeLogID { get; set; }
        public int? SessionID { get; set; }
        public AIEditEntityType EntityType { get; set; }
        public string EntityTypeName => EntityType.ToString();
        public int EntityRecordID { get; set; }
        public int CountryID { get; set; }
        public int Year { get; set; }
        public int? PillarID { get; set; }
        public int? QuestionID { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public int ChangedBy { get; set; }
        public string? ChangedByName { get; set; }
        public DateTime ChangedAt { get; set; }
        public Guid SaveBatchID { get; set; }
        public bool IsPublished { get; set; }
        public string ChangeSource { get; set; } = string.Empty;
    }

    public class AIEditSessionDetailDto
    {
        public AIEditSessionDto Session { get; set; } = new();
        public List<AIEditChangeCompareDto> Changes { get; set; } = new();
    }

    public class AIEditAccessDto
    {
        public bool CanEdit { get; set; }
        public bool HasPendingDraft { get; set; }
        public int? PermissionID { get; set; }
        public int? SessionID { get; set; }
        public string? PermissionStatus { get; set; }
        public string? SessionStatus { get; set; }
        public string? Message { get; set; }
    }

    public class AIEditHistoryRequestDto
    {
        public int? CountryID { get; set; }
        public int? Year { get; set; }
        public int? SessionID { get; set; }
        public int? UserID { get; set; }
        public byte? EntityType { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
