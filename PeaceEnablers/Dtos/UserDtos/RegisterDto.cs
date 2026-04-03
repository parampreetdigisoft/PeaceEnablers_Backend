using PeaceEnablers.Models;

namespace PeaceEnablers.Dtos.UserDtos
{
    public class RegisterDto
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Password { get; set; } = "sdfjru32brjfew";
        public UserRole Role { get; set; }
    }
    public class InviteUserDto : RegisterDto
    {
        public int InvitedUserID { get; set; }
        public List<int> CountryID { get; set; } = new();

    }

    public class InviteBulkUserDto
    {
        public List<InviteUserDto> users { get; set; }
    }
    public class UpdateInviteUserDto : InviteUserDto
    {
        public int UserID { get; set; }
    }
}
