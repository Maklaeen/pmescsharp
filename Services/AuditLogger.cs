using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;

namespace PmesCSharp.Services;

public sealed class AuditLogger : IAuditLogger
{
    private readonly AppDbContext _db;
    private readonly ICurrentCompany _currentCompany;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogger(
        AppDbContext db,
        ICurrentCompany currentCompany,
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _currentCompany = currentCompany;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
    }

    public IQueryable<AuditLog> Query() => _db.AuditLogs.AsNoTracking()
        .Where(a => a.CompanyId == _currentCompany.CompanyId)
        .OrderByDescending(a => a.Id);
    public IQueryable<AuditLog> QueryAll() => _db.AuditLogs.IgnoreQueryFilters().AsNoTracking().OrderByDescending(a => a.Id);

    public async Task LogAsync(
        string action,
        string? entityType = null,
        string? entityId = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0)
            return;

        var http = _httpContextAccessor.HttpContext;
        var actor = http?.User is null ? null : await _userManager.GetUserAsync(http.User);

        var log = new AuditLog
        {
            CompanyId = companyId,
            ActorUserId = actor?.Id,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            IpAddress = http?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = http?.Request.Headers.UserAgent.ToString(),
            CreatedAt = DateTime.UtcNow,
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
