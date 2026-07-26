using DsgOmnichannel.Domain.Entities;
using DsgOmnichannel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DsgOmnichannel.Api.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var items = await dbContext.StoreInventories
            .OrderBy(i => i.ProductId)
            .Select(i => new InventoryItemResponse(i.Id, i.StoreId, i.ProductId, i.Quantity))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertInventoryRequest request, CancellationToken cancellationToken)
    {
        var existing = await dbContext.StoreInventories
            .FirstOrDefaultAsync(i => i.StoreId == request.StoreId && i.ProductId == request.ProductId, cancellationToken);

        if (existing is not null)
        {
            existing.Quantity = request.Quantity;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new InventoryItemResponse(existing.Id, existing.StoreId, existing.ProductId, existing.Quantity));
        }

        var item = new StoreInventory
        {
            StoreId = request.StoreId,
            ProductId = request.ProductId,
            Quantity = request.Quantity
        };

        dbContext.StoreInventories.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/api/inventory/{item.Id}", new InventoryItemResponse(item.Id, item.StoreId, item.ProductId, item.Quantity));
    }

    [HttpPatch("{id:guid}/quantity")]
    public async Task<IActionResult> UpdateQuantity(Guid id, [FromBody] UpdateQuantityRequest request, CancellationToken cancellationToken)
    {
        var item = await dbContext.StoreInventories.FindAsync([id], cancellationToken);
        if (item is null)
            return NotFound();

        item.Quantity = request.Quantity;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new InventoryItemResponse(item.Id, item.StoreId, item.ProductId, item.Quantity));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var item = await dbContext.StoreInventories.FindAsync([id], cancellationToken);
        if (item is null)
            return NotFound();

        var orders = dbContext.Orders.Where(o => o.ProductId == item.ProductId && o.StoreId == item.StoreId);
        dbContext.Orders.RemoveRange(orders);
        dbContext.StoreInventories.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}

public record InventoryItemResponse(Guid Id, string StoreId, string ProductId, int Quantity);

public class UpsertInventoryRequest
{
    public string StoreId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class UpdateQuantityRequest
{
    public int Quantity { get; set; }
}
