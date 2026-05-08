namespace PeaceEnablers.Models
{
    public class DocumentChunks
    {
        public string ChunkID { get; set; } = string.Empty;

        public int CountryDocumentID { get; set; }

        public int TOCID { get; set; }

        public int CountryID { get; set; }

        public int PillarID { get; set; }

        public int ChunkIndex { get; set; }

        public string? ChunkText { get; set; }

        public int? TokenCount { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
