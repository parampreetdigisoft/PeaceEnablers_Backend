using DocumentFormat.OpenXml.Bibliography;

namespace PeaceEnablers.Models
{
    public class CountryDocument
    {
        public int CountryDocumentID { get; set; }
        public int? CountryID { get; set; } // if country id is null then documen treated as global docx for the site 
        public int? PillarID { get; set; }
        public string FileName { get; set; }
        public string StoredFileName { get; set; }
        public string FilePath { get; set; }
        public string FileType { get; set; }
        public long? FileSize { get; set; }
        public DocumentProcessingStatus ProcessingStatus { get; set; } = DocumentProcessingStatus.Pending;
        public int? UploadedByUserID { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string DocumentLevel { get; set; } // Gloabal,Country,Country_Pillar
    }

    public enum DocumentProcessingStatus
    {
        Pending = 0,
        Processing = 1,
        Completed = 2,
        Failed = 3
    }
}
