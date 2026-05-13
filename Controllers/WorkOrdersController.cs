using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "superadmin,admin,planner,operator")]
public class WorkOrdersController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentCompany _currentCompany;

    public WorkOrdersController(AppDbContext db, UserManager<ApplicationUser> userManager, ICurrentCompany currentCompany)
    {
        _db = db;
        _userManager = userManager;
        _currentCompany = currentCompany;
    }

    [HttpGet("/production/work-orders")]
    public async Task<IActionResult> Index([FromQuery] int page = 1)
    {
        const int pageSize = 15;
        var query = _db.WorkOrders
            .Include(w => w.ProductionSchedule).ThenInclude(s => s.Product)
            .Include(w => w.AssignedToUser)
            .OrderByDescending(w => w.Id);

        var workOrders = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var total = await query.CountAsync();
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        return View(workOrders);
    }

    [HttpGet("/production/work-orders/{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var wo = await _db.WorkOrders
            .Include(w => w.ProductionSchedule).ThenInclude(s => s.Product)
            .Include(w => w.AssignedToUser)
            .Include(w => w.MaterialMovements).ThenInclude(m => m.Material)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (wo is null) return NotFound();

        var operators = await _userManager.GetUsersInRoleAsync("operator");
        ViewBag.AvailableOperators = operators;
        ViewBag.Materials = await _db.Materials.Where(m => m.Status == "active").OrderBy(m => m.MaterialName).ToListAsync();
        return View(wo);
    }

    [HttpPost("/production/work-orders/{id:int}/claim")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Claim(int id)
    {
        var wo = await _db.WorkOrders.FindAsync(id);
        if (wo is null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        wo.AssignedToUserId = user?.Id;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Work order claimed.";
        return Redirect($"/production/work-orders/{id}");
    }

    [HttpPost("/production/work-orders/{id:int}/start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int id)
    {
        var wo = await _db.WorkOrders.FindAsync(id);
        if (wo is null) return NotFound();

        wo.Status = "ongoing";
        wo.StartedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Work order started.";
        return Redirect($"/production/work-orders/{id}");
    }

    [HttpPost("/production/work-orders/{id:int}/finish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Finish(int id, [FromForm] int actualQty)
    {
        var wo = await _db.WorkOrders.FindAsync(id);
        if (wo is null) return NotFound();

        wo.ActualQty = actualQty;
        wo.Status = "done";
        wo.FinishedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Work order marked done.";
        return Redirect($"/production/work-orders/{id}");
    }

    [HttpPost("/production/work-orders/{id:int}/cancel")]
    [Authorize(Roles = "superadmin,admin,planner")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var wo = await _db.WorkOrders.FindAsync(id);
        if (wo is null) return NotFound();

        wo.Status = "cancelled";
        wo.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Work order cancelled.";
        return Redirect($"/production/work-orders/{id}");
    }

    [HttpPost("/production/work-orders/{id:int}/assign")]
    [Authorize(Roles = "superadmin,admin,planner")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, [FromForm] string? assignedToUserId)
    {
        var wo = await _db.WorkOrders.FindAsync(id);
        if (wo is null) return NotFound();

        wo.AssignedToUserId = string.IsNullOrWhiteSpace(assignedToUserId) ? null : assignedToUserId;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Operator assigned.";
        return Redirect($"/production/work-orders/{id}");
    }

    [HttpPost("/production/work-orders/{id:int}/materials")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StoreMaterial(int id, [FromForm] int materialId, [FromForm] decimal quantity)
    {
        var wo = await _db.WorkOrders.Include(w => w.ProductionSchedule).FirstOrDefaultAsync(w => w.Id == id);
        if (wo is null) return NotFound();

        var material = await _db.Materials.FindAsync(materialId);
        if (material is null) return NotFound();

        var user = await _userManager.GetUserAsync(User);

        _db.MaterialMovements.Add(new MaterialMovement
        {
            CompanyId = _currentCompany.CompanyId,
            MaterialId = materialId,
            MovementType = "out",
            Quantity = quantity,
            Unit = material.Unit,
            ReferenceType = "work_order",
            WorkOrderId = id,
            ProductionScheduleId = wo.ProductionScheduleId,
            CreatedByUserId = user?.Id,
        });

        material.StockQuantity -= quantity;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Material usage recorded.";
        return Redirect($"/production/work-orders/{id}");
    }
}
