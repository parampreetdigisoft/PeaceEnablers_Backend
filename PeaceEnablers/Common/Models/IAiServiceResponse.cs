namespace PeaceEnablers.Common.Models
{
    public interface IAiServiceResponse
    {
        bool Success { get; set; }
        string? Message { get; set; }
    }
}
