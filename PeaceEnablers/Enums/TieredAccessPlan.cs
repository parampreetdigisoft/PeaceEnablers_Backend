namespace PeaceEnablers.Enums
{
    public enum TieredAccessPlan : byte  // maps well to SQL tinyint
    {
        Pending = 0,
        Basic = 1, 
        Standard = 2, 
        Premium = 3 
    }
    public enum ExportType
    {
        Excel = 1,
        Pdf = 2
    }
}
