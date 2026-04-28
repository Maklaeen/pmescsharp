using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "superadmin,admin,qc")]
public class QualityInspectionsController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public QualityInspectionsController(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet("/qc/inspections")]
    public async Task<IActionResult> Index([FromQuery] int page = 1)
    {
        const int pageSize = 15;
        var schedules = await _db.ProductionSchedules
            .Include(s => s.Product)
            .Include(s => s.QualityChecks)
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

    [HttpPost("/qc/inspections/{scheduleId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Store(int scheduleId, [FromForm] string result,
        [FromForm] int qtyPassed, [FromForm] int qtyFailed, [FromForm] string? remarks)
    {
        var user = await _userManager.GetUserAsync(User);

        _db.QualityChecks.Add(new QualityCheck
        {
            ProductionScheduleId = scheduleId,
            InspectedByUserId = user?.Id,
            Result = result,
            QtyPassed = qtyPassed,
            QtyFailed = qtyFailed,
            Remarks = remarks,
            InspectedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = "QC result saved.";
        return Redirect($"/production/schedules/{scheduleId}");
    }
}
