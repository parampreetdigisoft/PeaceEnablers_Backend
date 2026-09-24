using PeaceEnablers.Models;

namespace PeaceEnablers.Dtos.AiDto
{
    public class GetCountryDocumentResponseDto
    {
        public int? CountryID { get; set; }
        public string CountryName { get; set; }
        public int NoOfUsers { get; set; }
        public int NoOfFiles { get; set; }
        public string FileTypes { get; set; }
        public long? FilesSize { get; set; }       
    }

    public class GetCountryPillarDocumentResponseDto
    {
        public int CountryDocumentID { get; set; }
        public int? CountryID { get; set; }
        public int? PillarID { get; set; }
        public string? PillarName { get; set; }
        public string FileName { get; set; }
        public string StoredFileName { get; set; }
        public string FilePath { get; set; }
        public string FileType { get; set; }
        public long? FileSize { get; set; }
        public DocumentProcessingStatus ProcessingStatus { get; set; } = DocumentProcessingStatus.Pending;
        public int UploadedByUserID { get; set; }
        public string UploadedBy { get; set; }
        public string? DocumentLevel { get; set; }
        public string? Classification { get; set; }
        public int? RetentionDays { get; set; }
        public DateTime? IngestedAt { get; set; }
        public bool LegalHold { get; set; }
        public string? LifecycleStage { get; set; }
    }
}
