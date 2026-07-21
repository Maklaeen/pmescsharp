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
    private readonly PmesCSharp.Services.IAuditLogger _audit;

    public ProductsController(AppDbContext db, ICurrentCompany currentCompany, PmesCSharp.Services.EntitlementService entitlements, PmesCSharp.Services.IAuditLogger audit)
    {
        _db = db;
        _currentCompany = currentCompany;
        _entitlements = entitlements;
        _audit = audit;
    }

    [HttpGet("/admin/products")]
    public async Task<IActionResult> Index([FromQuery] int page = 1, [FromQuery] bool archived = false)
    {
        const int pageSize = 12;
        var productsQuery = _db.Products.AsNoTracking().Where(p => p.IsArchived == archived).OrderByDescending(p => p.Id);
        var products = await productsQuery.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var total = await _db.Products.CountAsync(p => p.IsArchived == archived);
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        ViewBag.ShowArchived = archived;
        ViewBag.ArchivedCount = await _db.Products.CountAsync(p => p.IsArchived);
        ViewBag.ActiveCount = await _db.Products.CountAsync(p => !p.IsArchived);
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

        var product = new Product
        {
            CompanyId = companyId,
            ProductCode = model.ProductCode,
            ProductName = model.ProductName,
            Description = model.Description,
            UnitPrice = model.UnitPrice,
            Unit = model.Unit,
            Status = model.Status,
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        try
        {
            await _audit.LogAsync("product.create", "Product", product.Id.ToString(), $"Added product {product.ProductName} ({product.ProductCode})");
        }
        catch { }

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

        var before = new { product.ProductCode, product.ProductName, product.Description, product.UnitPrice, product.Unit, product.Status };

        product.ProductCode = model.ProductCode;
        product.ProductName = model.ProductName;
        product.Description = model.Description;
        product.UnitPrice = model.UnitPrice;
        product.Unit = model.Unit;
        product.Status = model.Status;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        try
        {
            var details = $"Before: code={before.ProductCode},name={before.ProductName},price={before.UnitPrice}; After: code={product.ProductCode},name={product.ProductName},price={product.UnitPrice}";
            await _audit.LogAsync("UpdateProduct", "Product", product.Id.ToString(), details);
        }
        catch { }
        TempData["Success"] = "Product updated successfully.";
        return Redirect("/admin/products");
    }

    [HttpPost("/admin/products/{id:int}/status-archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();

        product.IsArchived = true;
        product.ArchivedAt = DateTime.UtcNow;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        try { await _audit.LogAsync("product.archive", "Product", product.Id.ToString(), $"Archived product {product.ProductName}"); } catch { }
        TempData["Success"] = "Product archived.";
        return Redirect("/admin/products");
    }

    [HttpPost("/admin/products/{id:int}/status-unarchive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unarchive(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();

        product.IsArchived = false;
        product.ArchivedAt = null;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        try { await _audit.LogAsync("product.unarchive", "Product", product.Id.ToString(), $"Unarchived product {product.ProductName}"); } catch { }
        TempData["Success"] = "Product unarchived.";
        return Redirect("/admin/products?archived=true");
    }
}
