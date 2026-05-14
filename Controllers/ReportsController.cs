using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "superadmin,admin,planner,qc,inventory,operator")]
public class ReportsController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICurrentCompany _currentCompany;

    public ReportsController(AppDbContext db, ICurrentCompany currentCompany)
    {
        _db = db;
        _currentCompany = currentCompany;
    }

    [HttpGet("/reports")]
    public IActionResult Index() => View();

    [HttpGet("/reports/production")]
    public async Task<IActionResult> Production([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        var fromDate = DateOnly.TryParse(from, out var f) ? f : DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1));
        var toDate = DateOnly.TryParse(to, out var t) ? t : DateOnly.FromDateTime(DateTime.UtcNow);

        var schedules = await _db.ProductionSchedules
            .Include(s => s.Product)
            .Include(s => s.WorkOrders)
            .Include(s => s.QualityChecks)
            .Include(s => s.ProductionCost)
            .Where(s => s.ScheduleDate >= fromDate && s.ScheduleDate <= toDate)
            .OrderByDescending(s => s.ScheduleDate)
            .ToListAsync(ct);

        ViewBag.From = fromDate.ToString("yyyy-MM-dd");
        ViewBag.To = toDate.ToString("yyyy-MM-dd");
        ViewBag.TotalSchedules = schedules.Count;
        ViewBag.Completed = schedules.Count(s => s.Status == "completed");
        ViewBag.InProgress = schedules.Count(s => s.Status == "in_progress");
        ViewBag.Cancelled = schedules.Count(s => s.Status == "cancelled");
        ViewBag.TotalPlannedQty = schedules.Sum(s => s.PlannedQuantity);
        ViewBag.TotalCost = schedules.Sum(s => s.ProductionCost?.TotalCost ?? 0);

        return View(schedules);
    }

    [HttpGet("/reports/costing")]
    public async Task<IActionResult> Costing([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        var fromDate = DateOnly.TryParse(from, out var f) ? f : DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1));
        var toDate = DateOnly.TryParse(to, out var t) ? t : DateOnly.FromDateTime(DateTime.UtcNow);

        var costs = await _db.ProductionCosts
            .Include(c => c.ProductionSchedule).ThenInclude(s => s.Product)
            .Include(c => c.ComputedByUser)
            .Where(c => c.ProductionSchedule.ScheduleDate >= fromDate && c.ProductionSchedule.ScheduleDate <= toDate)
            .OrderByDescending(c => c.ComputedAt)
            .ToListAsync(ct);

        ViewBag.From = fromDate.ToString("yyyy-MM-dd");
        ViewBag.To = toDate.ToString("yyyy-MM-dd");
        ViewBag.TotalMaterialCost = costs.Sum(c => c.MaterialCost);
        ViewBag.TotalLaborCost = costs.Sum(c => c.LaborCost);
        ViewBag.TotalCost = costs.Sum(c => c.TotalCost);

        return View(costs);
    }

    [HttpGet("/reports/quality")]
    public async Task<IActionResult> Quality([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        var fromDate = DateOnly.TryParse(from, out var f) ? f : DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1));
        var toDate = DateOnly.TryParse(to, out var t) ? t : DateOnly.FromDateTime(DateTime.UtcNow);

        var checks = await _db.QualityChecks
            .Include(q => q.ProductionSchedule).ThenInclude(s => s.Product)
            .Include(q => q.InspectedByUser)
            .Where(q => q.ProductionSchedule.ScheduleDate >= fromDate && q.ProductionSchedule.ScheduleDate <= toDate)
            .OrderByDescending(q => q.InspectedAt)
            .ToListAsync(ct);

        ViewBag.From = fromDate.ToString("yyyy-MM-dd");
        ViewBag.To = toDate.ToString("yyyy-MM-dd");
        ViewBag.TotalInspections = checks.Count;
        ViewBag.Passed = checks.Count(q => q.Result == "passed");
        ViewBag.Failed = checks.Count(q => q.Result == "failed");
        ViewBag.TotalQtyPassed = checks.Sum(q => q.QtyPassed);
        ViewBag.TotalQtyFailed = checks.Sum(q => q.QtyFailed);
        var passRate = checks.Count > 0 ? (double)checks.Count(q => q.Result == "passed") / checks.Count * 100 : 0;
        ViewBag.PassRate = Math.Round(passRate, 1);

        return View(checks);
    }
}
