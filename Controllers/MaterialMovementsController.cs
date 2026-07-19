using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "superadmin,admin,inventory")]
public class MaterialMovementsController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentCompany _currentCompany;

    public MaterialMovementsController(AppDbContext db, UserManager<ApplicationUser> userManager, ICurrentCompany currentCompany)
    {
        _db = db;
        _userManager = userManager;
        _currentCompany = currentCompany;
    }

    [HttpGet("/inventory/material-movements")]
    public async Task<IActionResult> Index([FromQuery] int page = 1)
    {
        const int pageSize = 20;
        var movements = await _db.MaterialMovements
            .Include(m => m.Material)
            .Include(m => m.CreatedByUser)
            .OrderByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var total = await _db.MaterialMovements.CountAsync();
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        ViewBag.Materials = await _db.Materials.Where(m => m.Status == "active").OrderBy(m => m.MaterialName).ToListAsync();
        return View(movements);
    }

    [HttpPost("/inventory/material-movements")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Store([FromForm] int materialId, [FromForm] string movementType,
        [FromForm] decimal quantity, [FromForm] string? referenceType, [FromForm] string? remarks)
    {
        var material = await _db.Materials.FindAsync(materialId);
        if (material is null)
        {
            TempData["Error"] = "Material not found.";
            return Redirect("/inventory/material-movements");
        }

        var user = await _userManager.GetUserAsync(User);

        _db.MaterialMovements.Add(new MaterialMovement
        {
            CompanyId = _currentCompany.CompanyId,
            MaterialId = materialId,
            MovementType = movementType,
            Quantity = quantity,
            Unit = material.Unit,
            ReferenceType = referenceType,
            Remarks = remarks,
            CreatedByUserId = user?.Id,
        });

        if (movementType == "in")
            material.StockQuantity += quantity;
        else
            material.StockQuantity -= quantity;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Movement saved.";
        return Redirect("/inventory/material-movements");
    }
}
