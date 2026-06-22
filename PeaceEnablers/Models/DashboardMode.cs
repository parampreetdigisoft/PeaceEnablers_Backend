namespace PeaceEnablers.Models
{
    public class DashboardMode
    {
        public int DashboardModeID { get; set; }
        public string ModeName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public UserRole? Role { get; set; }

        public ICollection<DashboardModeKPIMapping> DashboardModeKPIMappings { get; set; } = new List<DashboardModeKPIMapping>();
    }
}
