using DsgOmnichannel.Contracts.Events;
using DsgOmnichannel.Domain.Entities;
using DsgOmnichannel.Infrastructure.Persistence.Sagas;
using DsgOmnichannel.IntegrationTests.Infrastructure;
using DsgOmnichannel.Worker.Consumers;
using DsgOmnichannel.Worker.Sagas;

namespace DsgOmnichannel.IntegrationTests.Failures;

/// <summary>
/// Regression guard — Distributed Rollback / Compensating Transactions
///
/// Verifies that when inventory allocation fails (insufficient stock), the consumer
/// sets Order.Status = "AllocationFailed" and the saga finalizes (row deleted from
/// dbo.OrderState). The system must not be left in a half-committed state.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class DistributedRollbackTests(ApiFactory factory)
{
    [Fact]
    public async Task OrderPlacedConsumer_WhenInventoryInsufficient_OrderFailedAndSagaFinalized()
    {
        // Arrange — consumer harness with OrderPlacedEventConsumer and the EF Core saga,
        // sharing the same SQL Server Testcontainer used by ApiFactory.
        await using var provider = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(opts =>
                opts.UseSqlServer(factory.ConnectionString))
            .AddLogging()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<OrderPlacedEventConsumer>();

                // Register the saga with EF Core persistence so OrderState rows can be
                // queried directly after the saga processes AllocationFailedEvent.
                x.AddSagaStateMachine<OrderStateMachine, OrderState>()
                    .EntityFrameworkRepository(r =>
                    {
                        r.ExistingDbContext<ApplicationDbContext>();
                        r.UseSqlServer();
                    });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var orderId = Guid.NewGuid();
        const string storeId = "STORE-R01";
        const string productId = "PROD-R01";

        // Seed — Order with Quantity=2 but StoreInventory.Quantity=0 (insufficient stock)
        using (var seed = provider.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Orders.Add(new Order
            {
                Id = orderId,
                StoreId = storeId,
                CustomerName = "Rollback Test",
                ProductId = productId,
                Quantity = 2,
                TotalAmount = 100m,
                Status = "Submitted"
            });
            db.StoreInventories.Add(new StoreInventory
            {
                StoreId = storeId,
                ProductId = productId,
                Quantity = 0  // insufficient — forces the consumer to publish AllocationFailedEvent
            });
            await db.SaveChangesAsync();
        }

        // Act — consumer sees 0 stock → sets Order.Status = AllocationFailed, publishes
        // AllocationFailedEvent → saga receives it and transitions to Faulted.
        await harness.Bus.Publish(new OrderPlacedEvent(
            orderId, storeId, "Rollback Test", productId, 2, 100m, DateTime.UtcNow));

        await harness.Consumed.Any<OrderPlacedEvent>();
        await harness.Consumed.Any<AllocationFailedEvent>();
        await Task.Delay(TimeSpan.FromSeconds(1)); // allow saga DB write to commit

        // Assert
        using (var verify = provider.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var order = await db.Orders.FindAsync(orderId);
            order.Should().NotBeNull();
            order!.Status.Should().Be("AllocationFailed",
                "consumer must set Order.Status = AllocationFailed when inventory is insufficient");

            // The saga receives AllocationFailedEvent, calls Finalize(), and the EF Core saga
            // repository deletes the OrderState row. Failed orders are terminal — the row
            // must not accumulate in dbo.OrderState indefinitely.
            var sagaState = await db.OrderStates.FindAsync(orderId);
            sagaState.Should().BeNull(
                "saga must be finalized and deleted from dbo.OrderState after AllocationFailed — " +
                "Finalize() on the AllocationFailed transition triggers SetCompletedWhenFinalized()");
        }

        await harness.Stop();
    }
}
