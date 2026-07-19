namespace PmesCSharp.ViewModels.Users;

public class PendingApprovalUserViewModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
    public string PendingRole { get; init; } = "";
    public DateTime? RequestedAt { get; init; }
}
