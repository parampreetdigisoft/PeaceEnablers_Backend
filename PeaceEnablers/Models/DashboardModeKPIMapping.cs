namespace PeaceEnablers.Models
{
    public class DashboardModeKPIMapping
    {
        public int DashboardModeKPIMappingID { get; set; }
        public int DashboardModeID { get; set; }
        public int LayerID { get; set; }
        public string? Description { get; set; }
        public int PriorityLevel { get; set; } = 1;
        public int? DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;

        public DashboardMode DashboardMode { get; set; } = default!;
    }
}
