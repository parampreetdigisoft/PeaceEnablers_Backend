namespace PeaceEnablers.Models
{
    public enum AIEditPermissionStatus : byte
    {
        PendingRequest = 0,
        Active = 1,
        Consumed = 2,
        Rejected = 3,
        Revoked = 4
    }

    public enum AIEditSessionStatus : byte
    {
        Draft = 0,
        Submitted = 1,
        Approved = 2,
        Rejected = 3,
        Cancelled = 4
    }

    public enum AIEditEntityType : byte
    {
        Country = 1,
        Pillar = 2,
        Question = 3,
        Citation = 4
    }

    /// <summary>
    /// Admin grants (or analyst requests) edit authority for a country+year.
    /// After the analyst submits the session for approval, permission becomes Consumed
    /// and a new grant is required to edit again.
    /// </summary>
    public class AIEditPermission
    {
        public int PermissionID { get; set; }
        public int UserID { get; set; }
        public int CountryID { get; set; }
        public int Year { get; set; }
        public AIEditPermissionStatus Status { get; set; } = AIEditPermissionStatus.PendingRequest;
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public int? GrantedBy { get; set; }
        public DateTime? GrantedAt { get; set; }
        public string? Notes { get; set; }
        public int? ActiveSessionID { get; set; }
    }

    /// <summary>
    /// One open workspace for an analyst across country / pillar / question screens
    /// until they finalize (submit) for admin approval.
    /// </summary>
    public class AIEditSession
    {
        public int SessionID { get; set; }
        public int PermissionID { get; set; }
        public int UserID { get; set; }
        public int CountryID { get; set; }
        public int Year { get; set; }
        public AIEditSessionStatus Status { get; set; } = AIEditSessionStatus.Draft;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SubmittedAt { get; set; }
        public int? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewComment { get; set; }

        public AIEditPermission? Permission { get; set; }
        public ICollection<AIEditChangeLog>? ChangeLogs { get; set; }
    }

    /// <summary>
    /// Field-level audit trail. Live AI tables stay untouched until session is Approved.
    /// Multiple saves overwrite conceptually via latest NewValue per field; each save still appends a row.
    /// </summary>
    public class AIEditChangeLog
    {
        public long ChangeLogID { get; set; }
        public int? SessionID { get; set; }
        public AIEditEntityType EntityType { get; set; }
        public int EntityRecordID { get; set; }
        public int CountryID { get; set; }
        public int Year { get; set; }
        public int? PillarID { get; set; }
        public int? QuestionID { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public int ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        public Guid SaveBatchID { get; set; }
        /// <summary>True when change was applied to live tables (admin direct edit or after approve).</summary>
        public bool IsPublished { get; set; }
        /// <summary>AdminDirect | AnalystDraft | PublishApply</summary>
        public string ChangeSource { get; set; } = "AnalystDraft";

        public AIEditSession? Session { get; set; }
    }
}
