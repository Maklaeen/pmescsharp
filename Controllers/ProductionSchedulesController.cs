using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "superadmin,admin,planner,inventory,qc")]
public class ProductionSchedulesController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProductionSchedulesController(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet("/production/schedules")]
    public async Task<IActionResult> Index([FromQuery] int page = 1)
    {
        const int pageSize = 15;
        var schedules = await _db.ProductionSchedules
            .Include(s => s.Product)
            .Include(s => s.WorkOrders)
            .Include(s => s.QualityChecks)
            .OrderByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var total = await _db.ProductionSchedules.CountAsync();
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        ViewBag.CanManage = User.IsInRole("superadmin") || User.IsInRole("admin") || User.IsInRole("planner");
        return View(schedules);
    }

    [HttpGet("/production/schedules/create")]
    [Authorize(Roles = "superadmin,admin,planner")]
    public async Task<IActionResult> Create()
    {
        ViewBag.Products = await _db.Products.Where(p => p.Status == "active").OrderBy(p => p.ProductName).ToListAsync();
        return View();
    }

    [HttpPost("/production/schedules/create")]
    [Authorize(Roles = "superadmin,admin,planner")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Store([FromForm] int productId, [FromForm] int plannedQuantity,
        [FromForm] string scheduleDate, [FromForm] string? expectedEndAt)
    {
        if (productId == 0 || plannedQuantity < 1 || string.IsNullOrWhiteSpace(scheduleDate))
        {
            TempData["Error"] = "Please fill in all required fields.";
            ViewBag.Products = await _db.Products.Where(p => p.Status == "active").OrderBy(p => p.ProductName).ToListAsync();
            return View("Create");
        }

        var user = await _userManager.GetUserAsync(User);
        var schedule = new ProductionSchedule
        {
            ProductId = productId,
            PlannedQuantity = plannedQuantity,
            ScheduleDate = DateOnly.Parse(scheduleDate),
            CreatedByUserId = user?.Id,
            ExpectedEndAt = string.IsNullOrWhiteSpace(expectedEndAt) ? null : DateTime.Parse(expectedEndAt),
        };

        _db.ProductionSchedules.Add(schedule);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Schedule created.";
        return Redirect($"/production/schedules/{schedule.Id}");
    }

    [HttpGet("/production/schedules/{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var schedule = await _db.ProductionSchedules
            .Include(s => s.Product)
            .Include(s => s.CreatedByUser)
            .Include(s => s.WorkOrders).ThenInclude(w => w.AssignedToUser)
            .Include(s => s.QualityChecks).ThenInclude(q => q.InspectedByUser)
            .Include(s => s.ProductionCost)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (schedule is null) return NotFound();

        // MRP
        var bomLines = await _db.BillOfMaterials
            .Include(b => b.Material)
            .Where(b => b.ProductId == schedule.ProductId)
            .ToListAsync();

        var mrp = bomLines.Select(b => new
        {
            Material = b.Material,
            Required = b.QuantityRequired * schedule.PlannedQuantity,
            Available = b.Material?.StockQuantity ?? 0,
            Shortage = Math.Max(0, (b.QuantityRequired * schedule.PlannedQuantity) - (b.Material?.StockQuantity ?? 0)),
            Unit = b.Unit,
            UnitCost = b.Material?.UnitCost ?? 0,
            RequiredCost = b.QuantityRequired * schedule.PlannedQuantity * (b.Material?.UnitCost ?? 0),
        }).ToList();

        ViewBag.Mrp = mrp;
        ViewBag.CanManage = User.IsInRole("superadmin") || User.IsInRole("admin") || User.IsInRole("planner");
        ViewBag.CanCancel = User.IsInRole("superadmin") || User.IsInRole("admin");
        ViewBag.CanQc = User.IsInRole("superadmin") || User.IsInRole("admin") || User.IsInRole("qc");
        ViewBag.CanCost = User.IsInRole("superadmin") || User.IsInRole("admin") || User.IsInRole("planner");
        ViewBag.CanInventory = User.IsInRole("superadmin") || User.IsInRole("admin") || User.IsInRole("inventory");
        return View(schedule);
    }

    [HttpPost("/production/schedules/{id:int}/generate-work-orders")]
    [Authorize(Roles = "superadmin,admin,planner")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateWorkOrders(int id)
    {
        var schedule = await _db.ProductionSchedules.Include(s => s.WorkOrders).FirstOrDefaultAsync(s => s.Id == id);
        if (schedule is null) return NotFound();

        if (schedule.WorkOrders.Count > 0)
        {
            TempData["Error"] = "Work orders already exist for this schedule.";
            return Redirect($"/production/schedules/{id}");
        }

        var steps = new[] { "Printing", "Cutting", "Finishing", "Packaging" };
        foreach (var (step, i) in steps.Select((s, i) => (s, i)))
        {
            _db.WorkOrders.Add(new WorkOrder
            {
                ProductionScheduleId = id,
                WorkOrderNo = $"WO-{id:D4}-{(i + 1):D2}",
                ProcessStep = step,
                PlannedQty = schedule.PlannedQuantity,
            });
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Work orders generated.";
        return Redirect($"/production/schedules/{id}");
    }

    [HttpGet("/production/schedules/{id:int}/edit")]
    [Authorize(Roles = "superadmin,admin,planner")]
    public async Task<IActionResult> Edit(int id)
    {
        var schedule = await _db.ProductionSchedules.Include(s => s.Product).FirstOrDefaultAsync(s => s.Id == id);
        if (schedule is null) return NotFound();
        if (schedule.Status != "planned")
        {
            TempData["Error"] = "Only planned schedules can be edited.";
            return Redirect($"/production/schedules/{id}");
        }
        ViewBag.Products = await _db.Products.Where(p => p.Status == "active").OrderBy(p => p.ProductName).ToListAsync();
        ViewBag.ScheduleId = id;
        ViewBag.CurrentProductId = schedule.ProductId;
        ViewBag.CurrentPlannedQty = schedule.PlannedQuantity;
        ViewBag.CurrentScheduleDate = schedule.ScheduleDate.ToString("yyyy-MM-dd");
        ViewBag.CurrentExpectedEndAt = schedule.ExpectedEndAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
        return View();
    }

    [HttpPost("/production/schedules/{id:int}/edit")]
    [Authorize(Roles = "superadmin,admin,planner")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, [FromForm] int productId, [FromForm] int plannedQuantity,
        [FromForm] string scheduleDate, [FromForm] string? expectedEndAt)
    {
        var schedule = await _db.ProductionSchedules.FindAsync(id);
        if (schedule is null) return NotFound();
        if (schedule.Status != "planned")
        {
            TempData["Error"] = "Only planned schedules can be edited.";
            return Redirect($"/production/schedules/{id}");
        }

        schedule.ProductId = productId;
        schedule.PlannedQuantity = plannedQuantity;
        schedule.ScheduleDate = DateOnly.Parse(scheduleDate);
        schedule.ExpectedEndAt = string.IsNullOrWhiteSpace(expectedEndAt) ? null : DateTime.Parse(expectedEndAt);
        schedule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Schedule updated.";
        return Redirect($"/production/schedules/{id}");
    }

    [HttpPost("/production/schedules/{id:int}/start")]
    [Authorize(Roles = "superadmin,admin,planner")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int id)
    {
        var schedule = await _db.ProductionSchedules.FindAsync(id);
        if (schedule is null) return NotFound();

        schedule.Status = "in_progress";
        schedule.StartedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Schedule started.";
        return Redirect($"/production/schedules/{id}");
    }

    [HttpPost("/production/schedules/{id:int}/complete")]
    [Authorize(Roles = "superadmin,admin,planner")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        var schedule = await _db.ProductionSchedules.FindAsync(id);
        if (schedule is null) return NotFound();

        schedule.Status = "completed";
        schedule.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Schedule completed.";
        return Redirect($"/production/schedules/{id}");
    }

    [HttpPost("/production/schedules/{id:int}/cancel")]
    [Authorize(Roles = "superadmin,admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var schedule = await _db.ProductionSchedules.FindAsync(id);
        if (schedule is null) return NotFound();

        schedule.Status = "cancelled";
        schedule.CancelledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Schedule cancelled.";
        return Redirect($"/production/schedules/{id}");
    }
}
