namespace PmesCSharp.ViewModels.Dashboard;

public class AdminDashboardViewModel
{
    public int Users { get; init; }
    public int Products { get; init; }
    public int Materials { get; init; }
    public string WorkOrdersDisplay { get; init; } = "-";
}
