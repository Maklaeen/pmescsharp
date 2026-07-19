using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;

namespace PmesCSharp.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<Product>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _db.Products.OrderByDescending(p => p.Id).ToListAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Create([FromBody] Product input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.ProductName))
            return BadRequest("Product name is required.");

        var entity = new Product
        {
            ProductCode = input.ProductCode,
            ProductName = input.ProductName,
            Description = input.Description,
            UnitPrice = input.UnitPrice,
            Unit = input.Unit,
            Status = input.Status,
        };

        _db.Products.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Product>> Update(int id, [FromBody] Product input, CancellationToken cancellationToken)
    {
        var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (entity is null) return NotFound();

        if (string.IsNullOrWhiteSpace(input.ProductName))
            return BadRequest("Product name is required.");

        entity.ProductCode = input.ProductCode;
        entity.ProductName = input.ProductName;
        entity.Description = input.Description;
        entity.UnitPrice = input.UnitPrice;
        entity.Unit = input.Unit;
        entity.Status = input.Status;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(entity);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (entity is null) return NotFound();

        _db.Products.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
