using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "superadmin,admin,planner,qc")]
public class ProductionDashboardController : Controller
{
    private readonly AppDbContext _db;

    public ProductionDashboardController(AppDbContext db) => _db = db;

    [HttpGet("/production/dashboard")]
    [HttpGet("/production")]
    [HttpGet("/progress")]
    public async Task<IActionResult> Index([FromQuery] int page = 1)
    {
        const int pageSize = 10;
        var schedules = await _db.ProductionSchedules
            .Include(s => s.Product)
            .Include(s => s.WorkOrders)
            .Include(s => s.QualityChecks)
            .Where(s => s.Status == "in_progress")
            .OrderByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var total = await _db.ProductionSchedules.CountAsync(s => s.Status == "in_progress");
        ViewBag.InProgressCount = total;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        return View(schedules);
    }
}

[Authorize(Roles = "superadmin,admin,planner,qc,inventory")]
public class CostingController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public CostingController(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet("/costing")]
    public async Task<IActionResult> Index([FromQuery] int page = 1)
    {
        const int pageSize = 15;
        var schedules = await _db.ProductionSchedules
            .Include(s => s.Product)
            .Include(s => s.ProductionCost)
            .Where(s => s.Status == "in_progress" || s.Status == "completed")
            .OrderByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var total = await _db.ProductionSchedules.CountAsync(s => s.Status == "in_progress" || s.Status == "completed");
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        return View(schedules);
    }

    [HttpPost("/costing/{scheduleId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Compute(int scheduleId, [FromForm] decimal laborCost)
    {
        var schedule = await _db.ProductionSchedules
            .Include(s => s.ProductionCost)
            .FirstOrDefaultAsync(s => s.Id == scheduleId);
        if (schedule is null) return NotFound();

        var bomLines = await _db.BillOfMaterials
            .Include(b => b.Material)
            .Where(b => b.ProductId == schedule.ProductId)
            .ToListAsync();

        var materialCost = bomLines.Sum(b => b.QuantityRequired * schedule.PlannedQuantity * (b.Material?.UnitCost ?? 0));
        var totalCost = materialCost + laborCost;
        var costPerUnit = schedule.PlannedQuantity > 0 ? totalCost / schedule.PlannedQuantity : 0;

        var user = await _userManager.GetUserAsync(User);

        if (schedule.ProductionCost is null)
        {
            _db.ProductionCosts.Add(new ProductionCost
            {
                ProductionScheduleId = scheduleId,
                MaterialCost = materialCost,
                LaborCost = laborCost,
                TotalCost = totalCost,
                CostPerUnit = costPerUnit,
                ComputedByUserId = user?.Id,
                ComputedAt = DateTime.UtcNow,
            });
        }
        else
        {
            schedule.ProductionCost.MaterialCost = materialCost;
            schedule.ProductionCost.LaborCost = laborCost;
            schedule.ProductionCost.TotalCost = totalCost;
            schedule.ProductionCost.CostPerUnit = costPerUnit;
            schedule.ProductionCost.ComputedByUserId = user?.Id;
            schedule.ProductionCost.ComputedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Cost computed.";
        return Redirect($"/production/schedules/{scheduleId}");
    }
}
