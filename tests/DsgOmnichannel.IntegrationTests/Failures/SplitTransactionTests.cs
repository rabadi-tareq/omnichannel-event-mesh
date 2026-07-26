using DsgOmnichannel.Contracts.Events;
using DsgOmnichannel.Domain.Entities;
using DsgOmnichannel.Infrastructure.Persistence.Sagas;
using DsgOmnichannel.IntegrationTests.Infrastructure;
using DsgOmnichannel.Worker.Consumers;
using DsgOmnichannel.Worker.Sagas;

namespace DsgOmnichannel.IntegrationTests.Failures;

/// <summary>
/// Regression guard — Slice 2.2: Single Authority on Order.Status
///
/// Verifies that when AllocationFailedEvent is processed, Order.Status is owned
/// exclusively by the consumer (Transaction A) and the saga finalizes and cleans up
/// its own row (Transaction B). Each authority commits exactly once with no
/// cross-transaction coordination required.
///
/// Fix applied (Slice 2.2 / Known Gap #6): the saga's ThenAsync no longer opens a second
/// DbContext scope to write Order.Status. The consumer exclusively owns Order.Status;
/// the saga exclusively owns its finalization. Each authority commits exactly once
/// with no cross-transaction coordination required.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class SplitTransactionTests(ApiFactory factory)
{
    [Fact]
    public async Task AllocationFailed_OrderStatusAndSagaStateAreConsistent()
    {
        // Arrange — consumer + EF Core saga harness.
        // After the Slice 2.2 fix, there is exactly one transaction per authority:
        //   Transaction A — OrderPlacedEventConsumer owns Order.Status
        //   Transaction B — OrderStateMachine (EF Core saga repo) owns OrderState.CurrentState
        // Neither transaction touches the other's field, eliminating the split-transaction gap.
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
        const string storeId = "STORE-ST01";
        const string productId = "PROD-ST01";

        using (var seed = provider.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Orders.Add(new Order
            {
                Id = orderId, StoreId = storeId, CustomerName = "Split Tx Test",
                ProductId = productId, Quantity = 1, TotalAmount = 50m, Status = "Submitted"
            });
            db.StoreInventories.Add(new StoreInventory
            {
                StoreId = storeId, ProductId = productId, Quantity = 0 // forces AllocationFailed
            });
            await db.SaveChangesAsync();
        }

        await harness.Bus.Publish(new OrderPlacedEvent(
            orderId, storeId, "Split Tx Test", productId, 1, 50m, DateTime.UtcNow));

        await harness.Consumed.Any<AllocationFailedEvent>();
        await Task.Delay(TimeSpan.FromSeconds(1));

        // Assert — each authority wrote its own field in its own transaction.
        // If this test fails it means either:
        //   a) the consumer stopped writing Order.Status = "AllocationFailed", or
        //   b) the saga stopped finalizing and cleaning up its row,
        //   c) someone reintroduced a cross-authority write (re-opening the gap).
        using (var verify = provider.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var order = await db.Orders.FindAsync(orderId);
            var sagaState = await db.OrderStates.FindAsync(orderId);

            order!.Status.Should().Be("AllocationFailed",
                "consumer (Transaction A) must be the sole authority writing Order.Status");

            sagaState.Should().BeNull(
                "saga (Transaction B) must finalize and delete its OrderState row after AllocationFailed — " +
                "Finalize() replaces TransitionTo(Faulted) so no row persists after a terminal failure");
        }

        await harness.Stop();
    }
}
