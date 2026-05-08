namespace PeaceEnablers.Models
{
    public class DocumentTOC
    {
        public int TOCID { get; set; }

        public int CountryDocumentID { get; set; }

        public int CountryID { get; set; }

        public int PillarID { get; set; }

        public string? SectionPath { get; set; }

        public string? SectionTitle { get; set; }

        public int? SectionLevel { get; set; }

        public int? PageStart { get; set; }

        public int? PageEnd { get; set; }

        public int? ChunkCount { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    
}
