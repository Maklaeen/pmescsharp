namespace PmesCSharp.ViewModels.Invites;

public class InviteListItemViewModel
{
    public int Id { get; init; }
    public string Email { get; init; } = "";
    public string Role { get; init; } = "";
    public string Code { get; init; } = "";
    public DateTime ExpiresAt { get; init; }
    public int UsesCount { get; init; }
    public int MaxUses { get; init; }
    public bool IsActive { get; init; }
}
