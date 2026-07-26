using DsgOmnichannel.Domain.Entities;
using DsgOmnichannel.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DsgOmnichannel.IntegrationTests.Failures;

/// <summary>
/// Failure 6 — Concurrent Inventory Over-Allocation (Race Condition)
///
/// Verifies that two simultaneous orders for the same StoreId/ProductId cannot both
/// succeed when only one unit of stock exists. Without an optimistic concurrency token
/// (rowversion) on StoreInventory, both reads see Quantity = 1, both decrement to 0,
/// and stock goes negative — a known gap (SRS Known Gap #3).
///
/// This test will FAIL in its current state (exposing the gap). It becomes a regression
/// guard once the concurrency token and unique index are added.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class ConcurrentAllocationTests(ApiFactory factory)
{
    [Fact]
    public async Task TwoConcurrentOrders_WhenOnlyOneUnitInStock_OnlyOneOrderAllocated()
    {
        // Arrange — two ApplicationDbContext instances simulate two consumer instances
        // processing their events concurrently. Both read inventory BEFORE either
        // SaveChangesAsync call so both see Quantity = 1 (the race window).
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(factory.ConnectionString)
            .Options;

        var orderAId = Guid.NewGuid();
        var orderBId = Guid.NewGuid();
        const string storeId = "STORE-C01";
        const string productId = "PROD-C01";

        await using (var seed = new ApplicationDbContext(options))
        {
            seed.Orders.Add(new Order
            {
                Id = orderAId, StoreId = storeId, CustomerName = "Customer A",
                ProductId = productId, Quantity = 1, TotalAmount = 50m, Status = "Submitted"
            });
            seed.Orders.Add(new Order
            {
                Id = orderBId, StoreId = storeId, CustomerName = "Customer B",
                ProductId = productId, Quantity = 1, TotalAmount = 50m, Status = "Submitted"
            });
            seed.StoreInventories.Add(new StoreInventory
            {
                StoreId = storeId, ProductId = productId, Quantity = 1
            });
            await seed.SaveChangesAsync();
        }

        // Act — simulate the race: both contexts read inventory before either commits.
        // This is the exact window in OrderPlacedEventConsumer between FirstOrDefaultAsync
        // and SaveChangesAsync when a second consumer instance can sneak in.
        await using var dbA = new ApplicationDbContext(options);
        await using var dbB = new ApplicationDbContext(options);

        // ── both consumers read Quantity = 1 ──────────────────────────────────────
        var inventoryA = await dbA.StoreInventories
            .FirstAsync(i => i.StoreId == storeId && i.ProductId == productId);
        var orderA = await dbA.Orders.FindAsync(orderAId);

        var inventoryB = await dbB.StoreInventories
            .FirstAsync(i => i.StoreId == storeId && i.ProductId == productId);
        var orderB = await dbB.Orders.FindAsync(orderBId);

        // ── both compute 1 − 1 = 0 and mark orders Allocated ────────────────────
        inventoryA.Quantity -= 1;
        orderA!.Status = "Allocated";

        inventoryB.Quantity -= 1;  // stale read — still sees 1
        orderB!.Status = "Allocated";

        // ── both commit without conflict (no rowversion = no concurrency check) ──
        // Consumer A commits first; RowVersion on StoreInventory is incremented.
        await dbA.SaveChangesAsync();

        // Consumer B's commit now fails: the WHERE RowVersion = <original> clause
        // matches no row because Consumer A already changed it. EF Core throws
        // DbUpdateConcurrencyException. The retry policy re-reads Quantity = 0 and
        // routes to AllocationFailed instead of Allocated.
        try
        {
            await dbB.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Simulate what the retry would do: re-read Quantity = 0, fail allocation.
            await using var retryCtx = new ApplicationDbContext(options);
            var retryOrder = await retryCtx.Orders.FindAsync(orderBId);
            retryOrder!.Status = "AllocationFailed";
            await retryCtx.SaveChangesAsync();
        }

        // Assert — read final state from a clean context
        await using var verify = new ApplicationDbContext(options);

        var finalInventory = await verify.StoreInventories
            .FirstAsync(i => i.StoreId == storeId && i.ProductId == productId);

        finalInventory.Quantity.Should().Be(0,
            "Consumer A decremented Quantity to 0; Consumer B was rejected by the RowVersion check");

        var allocatedCount = await verify.Orders
            .CountAsync(o => (o.Id == orderAId || o.Id == orderBId) && o.Status == "Allocated");

        allocatedCount.Should().Be(1,
            "only one order can be allocated when only one unit of stock exists — " +
            "RowVersion on StoreInventory causes the second concurrent SaveChangesAsync to throw " +
            "DbUpdateConcurrencyException, which the retry policy re-routes to AllocationFailed");
    }
}
