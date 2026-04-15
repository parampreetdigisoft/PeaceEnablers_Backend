namespace PeaceEnablers.Dtos.AiDto
{
    public class UploadAiDocumentRequest
    {
        public int CountryID { get; set; }
        public List<IFormFile> Files { get; set; }
        public List<int> PillarIDs { get; set; } 
    }


}
