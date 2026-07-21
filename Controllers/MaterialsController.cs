using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.ViewModels.Materials;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "superadmin,admin")]
public class MaterialsController : Controller
{
    private readonly AppDbContext _db;
    private readonly PmesCSharp.Services.EntitlementService _entitlements;
    private readonly ICurrentCompany _currentCompany;
    private readonly PmesCSharp.Services.IAuditLogger _audit;

    public MaterialsController(AppDbContext db, PmesCSharp.Services.EntitlementService entitlements, ICurrentCompany currentCompany, PmesCSharp.Services.IAuditLogger audit)
    {
        _db = db;
        _entitlements = entitlements;
        _currentCompany = currentCompany;
        _audit = audit;
    }

    [HttpGet("/admin/materials")]
    public async Task<IActionResult> Index([FromQuery] int page = 1, [FromQuery] bool archived = false)
    {
        const int pageSize = 15;
        var materialsQuery = _db.Materials.AsNoTracking().Where(m => m.IsArchived == archived).OrderByDescending(m => m.Id);
        var materials = await materialsQuery.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var total = await _db.Materials.CountAsync(m => m.IsArchived == archived);
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        ViewBag.ShowArchived = archived;
        ViewBag.ArchivedCount = await _db.Materials.CountAsync(m => m.IsArchived);
        ViewBag.ActiveCount = await _db.Materials.CountAsync(m => !m.IsArchived);
        return View(materials);
    }

    [HttpGet("/admin/materials/create")]
    public IActionResult Create() => View(new MaterialFormViewModel());

    [HttpPost("/admin/materials")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Store(MaterialFormViewModel model)
    {
        if (!ModelState.IsValid) return View("Create", model);

        var canCreate = await _entitlements.EnsureCanCreateMaterialAsync();
        if (!canCreate.Allowed)
        {
            TempData["Error"] = canCreate.Message;
            return Redirect("/admin/materials");
        }

        var existing = await _db.Materials.AnyAsync(m => m.MaterialCode == model.MaterialCode);
        if (existing)
        {
            ModelState.AddModelError(nameof(model.MaterialCode), "Material code already exists.");
            return View("Create", model);
        }

        _db.Materials.Add(new Material
        {
            CompanyId = _currentCompany.CompanyId,
            MaterialCode = model.MaterialCode,
            MaterialName = model.MaterialName,
            Description = model.Description,
            Unit = model.Unit,
            UnitCost = model.UnitCost,
            StockQuantity = model.StockQuantity,
            ReorderLevel = model.ReorderLevel,
            Status = model.Status,
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = "Material created successfully.";
        return Redirect("/admin/materials");
    }

    [HttpGet("/admin/materials/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var material = await _db.Materials.FindAsync(id);
        if (material is null) return NotFound();

        ViewBag.MaterialId = id;
        return View(new MaterialFormViewModel
        {
            MaterialCode = material.MaterialCode,
            MaterialName = material.MaterialName,
            Description = material.Description,
            Unit = material.Unit,
            UnitCost = material.UnitCost,
            StockQuantity = material.StockQuantity,
            ReorderLevel = material.ReorderLevel,
            Status = material.Status,
        });
    }

    [HttpPost("/admin/materials/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, MaterialFormViewModel model)
    {
        ViewBag.MaterialId = id;
        if (!ModelState.IsValid) return View("Edit", model);

        var material = await _db.Materials.FindAsync(id);
        if (material is null) return NotFound();

        var duplicate = await _db.Materials.AnyAsync(m => m.MaterialCode == model.MaterialCode && m.Id != id);
        if (duplicate)
        {
            ModelState.AddModelError(nameof(model.MaterialCode), "Material code already exists.");
            return View("Edit", model);
        }

        var before = new { material.MaterialCode, material.MaterialName, material.UnitCost, material.StockQuantity };

        material.MaterialCode = model.MaterialCode;
        material.MaterialName = model.MaterialName;
        material.Description = model.Description;
        material.Unit = model.Unit;
        material.UnitCost = model.UnitCost;
        material.StockQuantity = model.StockQuantity;
        material.ReorderLevel = model.ReorderLevel;
        material.Status = model.Status;
        material.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        try
        {
            var details = $"Before: code={before.MaterialCode},name={before.MaterialName},cost={before.UnitCost},stock={before.StockQuantity}; After: code={material.MaterialCode},name={material.MaterialName},cost={material.UnitCost},stock={material.StockQuantity}";
            await _audit.LogAsync("UpdateMaterial", "Material", material.Id.ToString(), details);
        }
        catch { }
        TempData["Success"] = "Material updated successfully.";
        return Redirect("/admin/materials");
    }

    [HttpPost("/admin/materials/{id:int}/status-archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var material = await _db.Materials.FindAsync(id);
        if (material is null) return NotFound();

        material.IsArchived = true;
        material.ArchivedAt = DateTime.UtcNow;
        material.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        try { await _audit.LogAsync("material.archive", "Material", material.Id.ToString(), $"Archived material {material.MaterialName}"); } catch { }
        TempData["Success"] = "Material archived.";
        return Redirect("/admin/materials");
    }

    [HttpPost("/admin/materials/{id:int}/status-unarchive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unarchive(int id)
    {
        var material = await _db.Materials.FindAsync(id);
        if (material is null) return NotFound();

        material.IsArchived = false;
        material.ArchivedAt = null;
        material.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        try { await _audit.LogAsync("material.unarchive", "Material", material.Id.ToString(), $"Unarchived material {material.MaterialName}"); } catch { }
        TempData["Success"] = "Material unarchived.";
        return Redirect("/admin/materials?archived=true");
    }
}
