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

    public MaterialsController(AppDbContext db) => _db = db;

    [HttpGet("/admin/materials")]
    public async Task<IActionResult> Index([FromQuery] int page = 1)
    {
        const int pageSize = 15;
        var materials = await _db.Materials
            .OrderByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var total = await _db.Materials.CountAsync();
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        return View(materials);
    }

    [HttpGet("/admin/materials/create")]
    public IActionResult Create() => View(new MaterialFormViewModel());

    [HttpPost("/admin/materials/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Store(MaterialFormViewModel model)
    {
        if (!ModelState.IsValid) return View("Create", model);

        var existing = await _db.Materials.AnyAsync(m => m.MaterialCode == model.MaterialCode);
        if (existing)
        {
            ModelState.AddModelError(nameof(model.MaterialCode), "Material code already exists.");
            return View("Create", model);
        }

        _db.Materials.Add(new Material
        {
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
        TempData["Success"] = "Material updated successfully.";
        return Redirect("/admin/materials");
    }

    [HttpPost("/admin/materials/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Destroy(int id)
    {
        var material = await _db.Materials.FindAsync(id);
        if (material is not null)
        {
            _db.Materials.Remove(material);
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = "Material deleted.";
        return Redirect("/admin/materials");
    }
}
