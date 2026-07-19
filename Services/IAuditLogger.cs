using PmesCSharp.Models;

namespace PmesCSharp.Services;

public interface IAuditLogger
{
    Task LogAsync(string action, string? entityType = null, string? entityId = null, string? details = null, CancellationToken cancellationToken = default);
    IQueryable<AuditLog> Query();
    IQueryable<AuditLog> QueryAll();
}
