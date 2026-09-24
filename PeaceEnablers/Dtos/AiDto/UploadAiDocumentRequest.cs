namespace PeaceEnablers.Dtos.AiDto
{
    public class UploadAiDocumentRequest
    {
        public int? CountryID { get; set; }
        public List<IFormFile> Files { get; set; }
        public List<int> PillarIDs { get; set; }
        public string? Classification { get; set; }
        public int? RetentionDays { get; set; }
        public bool KeepIndefinitely { get; set; }
        public bool LegalHold { get; set; } 
    }


}
