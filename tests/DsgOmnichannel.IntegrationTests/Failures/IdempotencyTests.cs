using DsgOmnichannel.Contracts.Events;
using DsgOmnichannel.Domain.Entities;
using DsgOmnichannel.IntegrationTests.Infrastructure;
using DsgOmnichannel.Worker.Consumers;
using MassTransit;

namespace DsgOmnichannel.IntegrationTests.Failures;

/// <summary>
/// Failure 2 — Duplicate Processing / Non-Idempotency
///
/// Verifies that the MassTransit EF Core Inbox Pattern prevents double side-effects
/// when the broker redelivers the same OrderPlacedEvent multiple times (e.g., after a
/// transient consumer fault). Inventory must be decremented exactly once regardless of
/// how many times the message is delivered.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class IdempotencyTests(ApiFactory factory)
{
    [Fact]
    public async Task OrderPlacedConsumer_WhenEventDeliveredTwice_InventoryDecrementedOnlyOnce()
    {
        // Arrange — spin up a consumer harness with the EF Core Inbox pattern enabled,
        // mirroring the Worker's production setup (AddEntityFrameworkOutbox + UseEntityFrameworkOutbox).
        await using var provider = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(opts =>
                opts.UseSqlServer(factory.ConnectionString))
            .AddLogging()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<OrderPlacedEventConsumer>();

                // Inbox pattern: UseEntityFrameworkOutbox on the receive endpoint records
                // each MessageId in InboxState so that a redelivered message with the same
                // MessageId is silently skipped before the consumer body executes.
                x.AddEntityFrameworkOutbox<ApplicationDbContext>(o => o.UseSqlServer());

                x.AddConfigureEndpointsCallback((context, name, cfg) =>
                {
                    cfg.UseEntityFrameworkOutbox<ApplicationDbContext>(context);
                });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        // Seed — an Order and sufficient StoreInventory
        var orderId = Guid.NewGuid();
        const string storeId = "STORE-I01";
        const string productId = "PROD-I01";
        const int initialQuantity = 5;

        using (var seed = provider.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Orders.Add(new Order
            {
                Id = orderId,
                StoreId = storeId,
                CustomerName = "Idempotency Test",
                ProductId = productId,
                Quantity = 1,
                TotalAmount = 50m,
                Status = "Submitted"
            });
            db.StoreInventories.Add(new StoreInventory
            {
                StoreId = storeId,
                ProductId = productId,
                Quantity = initialQuantity
            });
            await db.SaveChangesAsync();
        }

        var @event = new OrderPlacedEvent(orderId, storeId, "Idempotency Test", productId, 1, 50m, DateTime.UtcNow);

        // Act — deliver the same event twice with an identical MessageId to simulate
        // broker redelivery (e.g., after a transient consumer fault/ack loss).
        var messageId = NewId.NextGuid();

        await harness.Bus.Publish(@event, ctx => ctx.MessageId = messageId);
        await harness.Consumed.Any<OrderPlacedEvent>();

        await harness.Bus.Publish(@event, ctx => ctx.MessageId = messageId);

        // Give the harness a moment to attempt the second delivery.
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Assert — inventory must have been decremented exactly once (5 → 4).
        // If the inbox did NOT protect us the quantity would be 3.
        using (var verify = provider.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var inventory = await db.StoreInventories
                .SingleAsync(i => i.StoreId == storeId && i.ProductId == productId);

            inventory.Quantity.Should().Be(initialQuantity - 1,
                "the EF Core Inbox Pattern must deduplicate redeliveries — inventory " +
                "must be decremented exactly once regardless of how many times the same " +
                "OrderPlacedEvent MessageId is delivered to the consumer");
        }

        await harness.Stop();
    }
}
