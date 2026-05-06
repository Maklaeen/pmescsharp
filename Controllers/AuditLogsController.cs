using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Services;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "superadmin,admin")]
public class AuditLogsController : Controller
{
    private readonly IAuditLogger _audit;

    public AuditLogsController(IAuditLogger audit)
    {
        _audit = audit;
    }

    [HttpGet("/admin/audit-logs")]
    public async Task<IActionResult> Index([FromQuery] int page = 1)
    {
        const int pageSize = 30;
        var query = _audit.Query();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var total = await query.CountAsync();
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        return View(items);
    }
}
