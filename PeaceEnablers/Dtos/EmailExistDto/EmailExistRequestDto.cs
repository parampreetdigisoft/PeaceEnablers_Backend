namespace PeaceEnablers.Dtos.EmailExistDto
{
    public class EmailExistRequestDto
    {
        public string Email { get; set; } = null!;

        public int ? UserID { get; set; } // Optional, used for update scenarios
    }
}
