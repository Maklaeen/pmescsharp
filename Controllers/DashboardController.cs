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

    public DashboardController(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
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
        var model = new AdminDashboardViewModel
        {
            Users = await _userManager.Users.CountAsync(),
            Products = await _db.Products.CountAsync(),
            Materials = await _db.Materials.CountAsync(),
            WorkOrdersDisplay = await _db.WorkOrders.CountAsync() is int c && c > 0 ? c.ToString() : "-",
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
