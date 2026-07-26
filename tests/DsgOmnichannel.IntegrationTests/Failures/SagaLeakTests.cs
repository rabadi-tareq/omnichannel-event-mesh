using DsgOmnichannel.Contracts.Events;
using DsgOmnichannel.Domain.Entities;
using DsgOmnichannel.Infrastructure.Persistence.Sagas;
using DsgOmnichannel.IntegrationTests.Infrastructure;
using DsgOmnichannel.Worker.Consumers;
using DsgOmnichannel.Worker.Sagas;

namespace DsgOmnichannel.IntegrationTests.Failures;

/// <summary>
/// Failure 7 — Never-Terminal Saga State Leak
///
/// Verifies that a completed happy-path order results in a finalized (removed) saga
/// instance in dbo.OrderState. The OrderStateMachine calls SetCompletedWhenFinalized()
/// on the OrderPickedUp path, meaning the row should be deleted after finalization.
/// Rows must not accumulate indefinitely (SRS Known Gap #2).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class SagaLeakTests(ApiFactory factory)
{
    [Fact]
    public async Task OrderSaga_WhenOrderPickedUp_SagaInstanceRemovedFromDatabase()
    {
        // Arrange — full happy-path harness: OrderPlacedEventConsumer allocates stock,
        // OrderPickedUpConsumer marks the order PickedUp, and the EF Core saga
        // transitions Processing → Finalized and deletes the OrderState row via
        // SetCompletedWhenFinalized().
        await using var provider = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(opts =>
                opts.UseSqlServer(factory.ConnectionString))
            .AddLogging()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<OrderPlacedEventConsumer>();
                x.AddConsumer<OrderPickedUpConsumer>();

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
        const string storeId = "STORE-S01";
        const string productId = "PROD-S01";

        using (var seed = provider.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Orders.Add(new Order
            {
                Id = orderId, StoreId = storeId, CustomerName = "Saga Happy Path",
                ProductId = productId, Quantity = 1, TotalAmount = 50m, Status = "Submitted"
            });
            db.StoreInventories.Add(new StoreInventory
            {
                StoreId = storeId, ProductId = productId, Quantity = 5
            });
            await db.SaveChangesAsync();
        }

        // Act — step 1: allocation succeeds → saga enters Processing state
        await harness.Bus.Publish(new OrderPlacedEvent(
            orderId, storeId, "Saga Happy Path", productId, 1, 50m, DateTime.UtcNow));

        // Wait for the consumer to finish (StoreInventoryAllocatedEvent proves it succeeded)
        // then allow the saga's Initially handler time to commit its own transaction.
        await harness.Published.Any<StoreInventoryAllocatedEvent>();
        await Task.Delay(TimeSpan.FromSeconds(1));

        // Act — step 2: order is picked up → saga transitions Finalized and row is deleted
        await harness.Bus.Publish(new OrderPickedUpEvent(
            orderId, storeId, "ASSOC-001", DateTime.UtcNow));

        await harness.Consumed.Any<OrderPickedUpEvent>();
        await Task.Delay(TimeSpan.FromSeconds(1)); // allow EF Core saga repo to commit deletion

        // Assert — the OrderState row must be gone.
        // SetCompletedWhenFinalized() instructs the EF Core saga repository to DELETE the row
        // when the saga reaches its terminal (Finalized) state. If this assertion fails, the
        // saga instance leaked and will accumulate in dbo.OrderState indefinitely (Known Gap #2).
        using (var verify = provider.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sagaState = await db.OrderStates.FindAsync(orderId);
            sagaState.Should().BeNull(
                "SetCompletedWhenFinalized() must delete the OrderState row when OrderPickedUp " +
                "finalizes the saga — rows must not accumulate in dbo.OrderState after happy-path completion");
        }

        await harness.Stop();
    }

    [Fact]
    public async Task OrderSaga_WhenAllocationFailed_SagaInstanceDoesNotAccumulate()
    {
        // Arrange — allocation-failure harness: consumer publishes AllocationFailedEvent,
        // saga transitions Processing → Finalized. SetCompletedWhenFinalized() deletes
        // the OrderState row, so dbo.OrderState must be empty after the event is consumed.
        await using var provider = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(opts =>
                opts.UseSqlServer(factory.ConnectionString))
            .AddLogging()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<OrderPlacedEventConsumer>();

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
        const string storeId = "STORE-S02";
        const string productId = "PROD-S02";

        using (var seed = provider.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Orders.Add(new Order
            {
                Id = orderId, StoreId = storeId, CustomerName = "Saga Leak Test",
                ProductId = productId, Quantity = 1, TotalAmount = 50m, Status = "Submitted"
            });
            db.StoreInventories.Add(new StoreInventory
            {
                StoreId = storeId, ProductId = productId, Quantity = 0  // insufficient — forces AllocationFailed
            });
            await db.SaveChangesAsync();
        }

        await harness.Bus.Publish(new OrderPlacedEvent(
            orderId, storeId, "Saga Leak Test", productId, 1, 50m, DateTime.UtcNow));

        await harness.Consumed.Any<AllocationFailedEvent>();
        await Task.Delay(TimeSpan.FromSeconds(1));

        // Assert — the OrderState row must be gone.
        // .Finalize() on the AllocationFailed transition triggers SetCompletedWhenFinalized(),
        // which instructs the EF Core saga repository to DELETE the row. Failed orders are
        // terminal — they must not accumulate in dbo.OrderState indefinitely.
        using (var verify = provider.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sagaState = await db.OrderStates.FindAsync(orderId);

            sagaState.Should().BeNull(
                "saga instance must be finalized and deleted from dbo.OrderState after AllocationFailed — " +
                "Faulted orders are terminal and must not accumulate indefinitely");
        }

        await harness.Stop();
    }
}
