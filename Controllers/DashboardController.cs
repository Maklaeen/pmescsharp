using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.ViewModels.Dashboard;

namespace PmesCSharp.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentCompany _currentCompany;

    public DashboardController(AppDbContext db, UserManager<ApplicationUser> userManager, ICurrentCompany currentCompany)
    {
        _db = db;
        _userManager = userManager;
       _currentCompany = currentCompany;
    }

    [HttpGet("/dashboard")]
    public IActionResult Index()
    {
        if (User.IsInRole("superadmin") || User.IsInRole("admin"))
        {
            return Redirect("/admin/dashboard");
        }

        if (User.IsInRole("planner"))
        {
            return Redirect("/planner/dashboard");
        }

        if (User.IsInRole("inventory"))
        {
            return Redirect("/inventory/dashboard");
        }

        if (User.IsInRole("operator"))
        {
            return Redirect("/operator/dashboard");
        }

        if (User.IsInRole("qc"))
        {
            return Redirect("/qc/dashboard");
        }

        return View("NoRole");
    }

    [Authorize(Roles = "superadmin,admin")]
    [HttpGet("/admin/dashboard")]
    public async Task<IActionResult> Admin()
    {
     var companyId = _currentCompany.CompanyId;
     if (!User.IsInRole("superadmin") && companyId <= 0)
            return Forbid();

        if (!User.IsInRole("superadmin"))
        {
            var hasSubscription = await _db.Set<CompanySubscription>().AnyAsync(s => s.CompanyId == companyId);
            if (!hasSubscription)
                return Redirect("/subscription/setup");
        }

        var isSuperAdmin = User.IsInRole("superadmin");

        var model = new AdminDashboardViewModel
        {
         Users = isSuperAdmin
                ? await _userManager.Users.CountAsync()
                : await _userManager.Users.Where(u => u.CompanyId == companyId).CountAsync(),
            Products = await _db.Products.CountAsync(),
            Materials = await _db.Materials.CountAsync(),
            WorkOrdersDisplay = await _db.WorkOrders.CountAsync() is int c && c > 0 ? c.ToString() : "-",
          CompanyName = isSuperAdmin
                ? "All Companies"
                : (await _db.Companies.Where(c => c.Id == companyId).Select(c => c.Name).FirstOrDefaultAsync() ?? ""),
            WorkOrdersInProgress = await _db.WorkOrders.CountAsync(w => w.Status != "done"),
            SchedulesInProgress = await _db.ProductionSchedules.CountAsync(s => s.Status == "in_progress"),
            SchedulesCompleted = await _db.ProductionSchedules.CountAsync(s => s.Status == "completed"),
        };

        return View(model);
    }

    [Authorize(Roles = "planner")]
    [HttpGet("/planner/dashboard")]
    public IActionResult Planner() => View();

    [Authorize(Roles = "inventory")]
    [HttpGet("/inventory/dashboard")]
    public IActionResult Inventory() => View();

    [Authorize(Roles = "operator")]
    [HttpGet("/operator/dashboard")]
    public IActionResult Operator() => View();

    [Authorize(Roles = "qc")]
    [HttpGet("/qc/dashboard")]
    public IActionResult Qc() => View();
}
