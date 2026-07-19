using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.ViewModels.Products;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "superadmin,admin")]
public class ProductsController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICurrentCompany _currentCompany;
    private readonly PmesCSharp.Services.EntitlementService _entitlements;

    public ProductsController(AppDbContext db, ICurrentCompany currentCompany, PmesCSharp.Services.EntitlementService entitlements)
    {
        _db = db;
        _currentCompany = currentCompany;
        _entitlements = entitlements;
    }

    [HttpGet("/admin/products")]
    public async Task<IActionResult> Index([FromQuery] int page = 1)
    {
        const int pageSize = 12;
        var products = await _db.Products
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var total = await _db.Products.CountAsync();
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        return View(products);
    }

    [HttpGet("/admin/products/create")]
    public IActionResult Create() => View(new ProductFormViewModel());

    [HttpPost("/admin/products")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Store(ProductFormViewModel model)
    {
        if (!ModelState.IsValid) return View("Create", model);

        var canCreate = await _entitlements.EnsureCanCreateProductAsync();
        if (!canCreate.Allowed)
        {
            TempData["Error"] = canCreate.Message;
            return Redirect("/admin/products");
        }

        var companyId = _currentCompany.CompanyId;
        var existing = await _db.Products.AnyAsync(p => p.ProductCode == model.ProductCode && p.CompanyId == companyId);
        if (existing)
        {
            ModelState.AddModelError(nameof(model.ProductCode), "Product code already exists.");
            return View("Create", model);
        }

        _db.Products.Add(new Product
        {
            CompanyId = companyId,
            ProductCode = model.ProductCode,
            ProductName = model.ProductName,
            Description = model.Description,
            UnitPrice = model.UnitPrice,
            Unit = model.Unit,
            Status = model.Status,
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = "Product created successfully.";
        return Redirect("/admin/products");
    }

    [HttpGet("/admin/products/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();

        var vm = new ProductFormViewModel
        {
            ProductCode = product.ProductCode,
            ProductName = product.ProductName,
            Description = product.Description,
            UnitPrice = product.UnitPrice,
            Unit = product.Unit,
            Status = product.Status,
        };

        ViewBag.ProductId = id;
        return View(vm);
    }

    [HttpPost("/admin/products/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, ProductFormViewModel model)
    {
        ViewBag.ProductId = id;
        if (!ModelState.IsValid) return View("Edit", model);

        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();

     var companyId = _currentCompany.CompanyId;
        var duplicate = await _db.Products.AnyAsync(p => p.ProductCode == model.ProductCode && p.Id != id && p.CompanyId == companyId);
        if (duplicate)
        {
            ModelState.AddModelError(nameof(model.ProductCode), "Product code already exists.");
            return View("Edit", model);
        }

        product.ProductCode = model.ProductCode;
        product.ProductName = model.ProductName;
        product.Description = model.Description;
        product.UnitPrice = model.UnitPrice;
        product.Unit = model.Unit;
        product.Status = model.Status;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Product updated successfully.";
        return Redirect("/admin/products");
    }

    [HttpPost("/admin/products/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Destroy(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is not null)
        {
            _db.Products.Remove(product);
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = "Product deleted.";
        return Redirect("/admin/products");
    }
}
