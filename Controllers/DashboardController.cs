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
            return Redirect("/admin");
        if (User.IsInRole("planner"))
            return Redirect("/planner");
        if (User.IsInRole("inventory"))
            return Redirect("/inventory");
        if (User.IsInRole("operator"))
            return Redirect("/operator");
        if (User.IsInRole("qc"))
            return Redirect("/qc");

        return View("NoRole");
    }

    [Authorize(Roles = "superadmin,admin")]
    [HttpGet("/admin/dashboard")]
    [HttpGet("/admin")]
    public async Task<IActionResult> Admin()
    {
        var companyId = _currentCompany.CompanyId;
        var isSuperAdmin = User.IsInRole("superadmin");

        CompanyProfile? profile = null;
        Company company = null!;
        if (!isSuperAdmin && companyId > 0)
        {
            company = await _db.Companies.AsNoTracking().FirstAsync(c => c.Id == companyId);
            profile = await _db.CompanyProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.CompanyId == companyId);
        }

        var usersCount = isSuperAdmin || companyId <= 0
            ? await _userManager.Users.CountAsync()
            : await _userManager.Users.Where(u => u.CompanyId == companyId).CountAsync();

        var productsCount = isSuperAdmin
            ? await _db.Products.IgnoreQueryFilters().CountAsync()
            : await _db.Products.CountAsync();
        var materialsCount = isSuperAdmin
            ? await _db.Materials.IgnoreQueryFilters().CountAsync()
            : await _db.Materials.CountAsync();
        var workOrdersCount = isSuperAdmin
            ? await _db.WorkOrders.IgnoreQueryFilters().CountAsync()
            : await _db.WorkOrders.CountAsync();
        var schedulesInProgress = isSuperAdmin
            ? await _db.ProductionSchedules.IgnoreQueryFilters().CountAsync(s => s.Status == "in_progress")
            : await _db.ProductionSchedules.CountAsync(s => s.Status == "in_progress");
        var schedulesCompleted = isSuperAdmin
            ? await _db.ProductionSchedules.IgnoreQueryFilters().CountAsync(s => s.Status == "completed")
            : await _db.ProductionSchedules.CountAsync(s => s.Status == "completed");
        var workOrdersInProgress = isSuperAdmin
            ? await _db.WorkOrders.IgnoreQueryFilters().CountAsync(w => w.Status == "ongoing")
            : await _db.WorkOrders.CountAsync(w => w.Status == "ongoing");
        var companiesCount = isSuperAdmin ? await _db.Companies.CountAsync() : 0;

        var model = new AdminDashboardViewModel
        {
            Users = usersCount,
            Products = productsCount,
            Materials = materialsCount,
            WorkOrdersDisplay = isSuperAdmin ? $"{workOrdersCount}" : workOrdersCount.ToString(),
            CompanyName = isSuperAdmin ? $"{companiesCount} companies" : (companyId <= 0 ? "" : company.Name),
            NeedsCompanyProfileSetup = !isSuperAdmin && companyId > 0 && profile is null,
            WorkOrdersInProgress = workOrdersInProgress,
            SchedulesInProgress = schedulesInProgress,
            SchedulesCompleted = schedulesCompleted,
        };

        return View(model);
    }

    [Authorize(Roles = "planner")]
    [HttpGet("/planner/dashboard")]
    [HttpGet("/planner")]
    public IActionResult Planner() => View();

    [Authorize(Roles = "inventory")]
    [HttpGet("/inventory/dashboard")]
    [HttpGet("/inventory")]
    public IActionResult Inventory() => View();

    [Authorize(Roles = "operator")]
    [HttpGet("/operator/dashboard")]
    [HttpGet("/operator")]
    public IActionResult Operator() => View();

    [Authorize(Roles = "qc")]
    [HttpGet("/qc/dashboard")]
    [HttpGet("/qc")]
    public IActionResult Qc() => View();
}
