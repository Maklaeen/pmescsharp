using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.ViewModels.Bom;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "superadmin,admin")]
public class BomController : Controller
{
    private readonly AppDbContext _db;

    public BomController(AppDbContext db) => _db = db;

    [HttpGet("/admin/bom")]
    public async Task<IActionResult> Index([FromQuery] int page = 1)
    {
        const int pageSize = 15;
        var lines = await _db.BillOfMaterials
            .Include(b => b.Product)
            .Include(b => b.Material)
            .OrderByDescending(b => b.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var total = await _db.BillOfMaterials.CountAsync();
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        return View(lines);
    }

    [HttpGet("/admin/bom/create")]
    public async Task<IActionResult> Create()
    {
        await LoadDropdowns();
        return View(new BomFormViewModel());
    }

    [HttpPost("/admin/bom/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Store(BomFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await LoadDropdowns();
            return View("Create", model);
        }

        var duplicate = await _db.BillOfMaterials
            .AnyAsync(b => b.ProductId == model.ProductId && b.MaterialId == model.MaterialId);
        if (duplicate)
        {
            ModelState.AddModelError(string.Empty, "This product-material combination already exists.");
            await LoadDropdowns();
            return View("Create", model);
        }

        var material = await _db.Materials.FindAsync(model.MaterialId);
        _db.BillOfMaterials.Add(new BillOfMaterial
        {
            ProductId = model.ProductId,
            MaterialId = model.MaterialId,
            QuantityRequired = model.QuantityRequired,
            Unit = string.IsNullOrWhiteSpace(model.Unit) ? (material?.Unit ?? "pcs") : model.Unit,
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = "BOM line added successfully.";
        return Redirect("/admin/bom");
    }

    [HttpGet("/admin/bom/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var line = await _db.BillOfMaterials.FindAsync(id);
        if (line is null) return NotFound();

        await LoadDropdowns();
        ViewBag.BomId = id;
        return View(new BomFormViewModel
        {
            ProductId = line.ProductId,
            MaterialId = line.MaterialId,
            QuantityRequired = line.QuantityRequired,
            Unit = line.Unit,
        });
    }

    [HttpPost("/admin/bom/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, BomFormViewModel model)
    {
        ViewBag.BomId = id;
        if (!ModelState.IsValid)
        {
            await LoadDropdowns();
            return View("Edit", model);
        }

        var line = await _db.BillOfMaterials.FindAsync(id);
        if (line is null) return NotFound();

        var duplicate = await _db.BillOfMaterials
            .AnyAsync(b => b.ProductId == model.ProductId && b.MaterialId == model.MaterialId && b.Id != id);
        if (duplicate)
        {
            ModelState.AddModelError(string.Empty, "This product-material combination already exists.");
            await LoadDropdowns();
            return View("Edit", model);
        }

        var material = await _db.Materials.FindAsync(model.MaterialId);
        line.ProductId = model.ProductId;
        line.MaterialId = model.MaterialId;
        line.QuantityRequired = model.QuantityRequired;
        line.Unit = string.IsNullOrWhiteSpace(model.Unit) ? (material?.Unit ?? "pcs") : model.Unit;
        line.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "BOM line updated.";
        return Redirect("/admin/bom");
    }

    [HttpPost("/admin/bom/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Destroy(int id)
    {
        var line = await _db.BillOfMaterials.FindAsync(id);
        if (line is not null)
        {
            _db.BillOfMaterials.Remove(line);
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = "BOM line deleted.";
        return Redirect("/admin/bom");
    }

    private async Task LoadDropdowns()
    {
        ViewBag.Products = await _db.Products.Where(p => p.Status == "active").OrderBy(p => p.ProductName).ToListAsync();
        ViewBag.Materials = await _db.Materials.Where(m => m.Status == "active").OrderBy(m => m.MaterialName).ToListAsync();
    }
}
