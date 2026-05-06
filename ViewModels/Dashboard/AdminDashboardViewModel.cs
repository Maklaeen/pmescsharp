namespace PmesCSharp.ViewModels.Dashboard;

public class AdminDashboardViewModel
{
    public int Users { get; init; }
    public int Products { get; init; }
    public int Materials { get; init; }
    public string WorkOrdersDisplay { get; init; } = "-";

    public string CompanyName { get; init; } = "";
    public int WorkOrdersInProgress { get; init; }
    public int SchedulesInProgress { get; init; }
    public int SchedulesCompleted { get; init; }
}
