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

        if (isSuperAdmin)
        {
            // Superadmin sees system-wide stats only
            var companiesCount = await _db.Companies.CountAsync();
            var usersCount = await _userManager.Users.CountAsync();
            var schedulesCompleted = await _db.ProductionSchedules.IgnoreQueryFilters().CountAsync(s => s.Status == "completed");
            var schedulesInProgress = await _db.ProductionSchedules.IgnoreQueryFilters().CountAsync(s => s.Status == "in_progress");
            var proSubs = await _db.CompanySubscriptions.IgnoreQueryFilters().CountAsync(s => s.Plan == SubscriptionPlan.Pro && s.Status == SubscriptionStatus.Active);

            // Per-company completed schedules for chart
            var companyStats = await _db.Companies
                .AsNoTracking()
                .Select(c => new
                {
                    c.Name,
                    Completed = _db.ProductionSchedules.IgnoreQueryFilters().Count(s => s.CompanyId == c.Id && s.Status == "completed"),
                    InProgress = _db.ProductionSchedules.IgnoreQueryFilters().Count(s => s.CompanyId == c.Id && s.Status == "in_progress"),
                    QcPassed = _db.QualityChecks.IgnoreQueryFilters().Count(q => q.ProductionSchedule.CompanyId == c.Id && q.Result == "passed"),
                })
                .ToListAsync();

            ViewBag.IsSuperAdmin = true;
            ViewBag.CompaniesCount = companiesCount;
            ViewBag.UsersCount = usersCount;
            ViewBag.SchedulesCompleted = schedulesCompleted;
            ViewBag.SchedulesInProgress = schedulesInProgress;
            ViewBag.ProSubs = proSubs;
            ViewBag.CompanyStats = companyStats;
            return View("SuperAdminDashboard");
        }

        CompanyProfile? profile = null;
        Company company = null!;
        if (companyId > 0)
        {
            company = await _db.Companies.AsNoTracking().FirstAsync(c => c.Id == companyId);
            profile = await _db.CompanyProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.CompanyId == companyId);
        }

        var model = new AdminDashboardViewModel
        {
            Users = await _userManager.Users.Where(u => u.CompanyId == companyId).CountAsync(),
            Products = await _db.Products.CountAsync(),
            Materials = await _db.Materials.CountAsync(),
            WorkOrdersDisplay = (await _db.WorkOrders.CountAsync()).ToString(),
            CompanyName = companyId <= 0 ? "" : company.Name,
            NeedsCompanyProfileSetup = companyId > 0 && profile is null,
            WorkOrdersInProgress = await _db.WorkOrders.CountAsync(w => w.Status == "ongoing"),
            SchedulesInProgress = await _db.ProductionSchedules.CountAsync(s => s.Status == "in_progress"),
            SchedulesCompleted = await _db.ProductionSchedules.CountAsync(s => s.Status == "completed"),
        };

        return View(model);
    }

    [Authorize(Roles = "planner")]
    [HttpGet("/planner/dashboard")]
    [HttpGet("/planner")]
    public async Task<IActionResult> Planner()
    {
        var companyId = _currentCompany.CompanyId;
        var schedules = await _db.ProductionSchedules
            .Include(s => s.WorkOrders)
            .OrderByDescending(s => s.Id)
            .Take(10)
            .ToListAsync();

        ViewBag.TotalSchedules = await _db.ProductionSchedules.CountAsync();
        ViewBag.InProgress = await _db.ProductionSchedules.CountAsync(s => s.Status == "in_progress");
        ViewBag.Completed = await _db.ProductionSchedules.CountAsync(s => s.Status == "completed");
        ViewBag.Planned = await _db.ProductionSchedules.CountAsync(s => s.Status == "planned");
        ViewBag.RecentSchedules = schedules;
        return View();
    }

    [Authorize(Roles = "inventory")]
    [HttpGet("/inventory/dashboard")]
    [HttpGet("/inventory")]
    public async Task<IActionResult> Inventory()
    {
        ViewBag.TotalMaterials = await _db.Materials.CountAsync();
        ViewBag.TotalMovements = await _db.MaterialMovements.CountAsync();
        ViewBag.StockIn = await _db.MaterialMovements.CountAsync(m => m.MovementType == "in");
        ViewBag.StockOut = await _db.MaterialMovements.CountAsync(m => m.MovementType == "out");
        ViewBag.LowStock = await _db.Materials.CountAsync(m => m.StockQuantity <= m.ReorderLevel && m.ReorderLevel > 0);
        ViewBag.RecentMovements = await _db.MaterialMovements
            .Include(m => m.Material)
            .OrderByDescending(m => m.Id)
            .Take(5)
            .ToListAsync();
        return View();
    }

    [Authorize(Roles = "operator")]
    [HttpGet("/operator/dashboard")]
    [HttpGet("/operator")]
    public async Task<IActionResult> Operator()
    {
        var user = await _userManager.GetUserAsync(User);
        var myWOs = await _db.WorkOrders
            .Include(w => w.ProductionSchedule).ThenInclude(s => s.Product)
            .Where(w => w.AssignedToUserId == user!.Id)
            .OrderByDescending(w => w.Id)
            .Take(10)
            .ToListAsync();

        ViewBag.MyTotal = myWOs.Count;
        ViewBag.MyOngoing = myWOs.Count(w => w.Status == "ongoing");
        ViewBag.MyDone = myWOs.Count(w => w.Status == "done");
        ViewBag.MyPending = myWOs.Count(w => w.Status == "pending");
        ViewBag.MyWorkOrders = myWOs;
        return View();
    }

    [Authorize(Roles = "qc")]
    [HttpGet("/qc/dashboard")]
    [HttpGet("/qc")]
    public async Task<IActionResult> Qc()
    {
        var checks = await _db.QualityChecks
            .Include(q => q.ProductionSchedule).ThenInclude(s => s.Product)
            .OrderByDescending(q => q.Id)
            .Take(10)
            .ToListAsync();

        ViewBag.TotalInspections = await _db.QualityChecks.CountAsync();
        ViewBag.Passed = await _db.QualityChecks.CountAsync(q => q.Result == "passed");
        ViewBag.Failed = await _db.QualityChecks.CountAsync(q => q.Result == "failed");
        ViewBag.PendingSchedules = await _db.ProductionSchedules.CountAsync(s => s.Status == "in_progress");
        ViewBag.RecentChecks = checks;
        return View();
    }
}
