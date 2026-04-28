using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PmesCSharp.Controllers;

[Authorize]
public class ModulesController : Controller
{
    [HttpGet("/admin/audit-logs")]
    [Authorize(Roles = "superadmin")]
    public IActionResult AuditLogs()
    {
        ViewData["Title"] = "Audit Logs";
        return View("~/Views/Shared/ModulePlaceholder.cshtml");
    }
}
