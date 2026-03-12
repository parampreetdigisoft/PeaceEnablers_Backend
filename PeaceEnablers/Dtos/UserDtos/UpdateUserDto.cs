using PeaceEnablers.Enums;
using PeaceEnablers.Models;

namespace PeaceEnablers.Dtos.UserDtos
{
    public class UpdateUserDto
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public IFormFile? ProfileImage { get; set; }
        public bool Is2FAEnabled { get; set; } = false;
    }
    public class UpdateUserResponseDto
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string? ProfileImagePath { get; set; }
        public bool Is2FAEnabled { get; set; } = false;
        public TieredAccessPlan Tier { get; set; } = TieredAccessPlan.Pending;
    }
}
