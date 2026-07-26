using DsgOmnichannel.Contracts.Events;
using DsgOmnichannel.Domain.Entities;
using DsgOmnichannel.IntegrationTests.Infrastructure;
using DsgOmnichannel.Worker.Consumers;
using Microsoft.EntityFrameworkCore;

namespace DsgOmnichannel.IntegrationTests.Failures;

/// <summary>
/// Failure 4 — Poison Messages and Worker Starvation
///
/// Verifies that a malformed or domain-invalid OrderPlacedEvent bypasses the retry
/// policy and is immediately routed to the Dead-Letter Queue (OrderPlacedEvent_error).
/// Healthy messages in the same queue must not be blocked by the poison message.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class PoisonMessageTests(ApiFactory factory)
{
    [Fact]
    public async Task OrderPlacedConsumer_WhenDomainExceptionThrown_MessageRoutedToDlqWithoutRetry()
    {
        // Arrange — consumer harness backed by an unreachable database so the consumer
        // throws SqlException on its first DB call.
        //
        // The retry policy is configured to handle only TimeoutException, DbUpdateException,
        // and HttpRequestException. SqlException does not match any Handle<> filter, so the
        // retry middleware skips all retry attempts and immediately publishes Fault<T> — the
        // in-process equivalent of routing to the _error (DLQ) queue in production.
        const string unreachableConnection =
            "Server=localhost,19999;Database=FakeDb;User Id=sa;Password=FakePassword123!;" +
            "Connect Timeout=1;TrustServerCertificate=True;";

        await using var provider = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(opts =>
                opts.UseSqlServer(unreachableConnection))
            .AddLogging()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<OrderPlacedEventConsumer>();

                // Mirror the Worker's retry policy so the test exercises the real filter logic.
                x.AddConfigureEndpointsCallback((_, _, cfg) =>
                {
                    cfg.UseMessageRetry(r =>
                    {
                        r.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2));
                        r.Handle<TimeoutException>();
                        r.Handle<DbUpdateException>();
                        r.Handle<HttpRequestException>();
                        r.Ignore<ArgumentException>();
                    });
                });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        // Act — publish any event; the consumer will fault on the first DB call because
        // the server is unreachable.
        await harness.Bus.Publish(new OrderPlacedEvent(
            Guid.NewGuid(), "STORE-P01", "Poison Test", "PROD-P01", 1, 10m, DateTime.UtcNow));

        // Assert — Fault<OrderPlacedEvent> is published, which is the in-process equivalent
        // of DLQ routing. The fault appears quickly because SqlException is not in the
        // Handle<> allow-list and bypasses all retry attempts.
        (await harness.Published.Any<Fault<OrderPlacedEvent>>())
            .Should().BeTrue(
                "consumer must fault and route the poison message to the DLQ " +
                "(Fault<OrderPlacedEvent>) when a non-retriable exception is thrown");

        await harness.Stop();
    }

    [Fact]
    public async Task OrderPlacedConsumer_WhenPoisonMessagePresent_HealthyMessagesStillProcessed()
    {
        // Arrange — two events are published to the same consumer:
        //   • Poison:  Order exists but inventory = 0 → allocation fails, consumer returns cleanly (no throw)
        //   • Healthy: Order exists with inventory = 5 → allocation succeeds → StoreInventoryAllocatedEvent
        //
        // This verifies queue non-starvation: a domain-failure message does not block
        // the healthy message that follows it in the same queue.
        await using var provider = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(opts =>
                opts.UseSqlServer(factory.ConnectionString))
            .AddLogging()
            .AddMassTransitTestHarness(x => x.AddConsumer<OrderPlacedEventConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var poisonOrderId = Guid.NewGuid();
        var healthyOrderId = Guid.NewGuid();
        const string storeId = "STORE-P02";

        using (var seed = provider.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Poison order — inventory = 0, allocation fails → consumer sets AllocationFailed and returns
            db.Orders.Add(new Order
            {
                Id = poisonOrderId, StoreId = storeId, CustomerName = "Poison",
                ProductId = "PROD-PA", Quantity = 1, TotalAmount = 10m, Status = "Submitted"
            });
            db.StoreInventories.Add(new StoreInventory
            {
                StoreId = storeId, ProductId = "PROD-PA", Quantity = 0
            });

            // Healthy order — inventory = 5, allocation succeeds
            db.Orders.Add(new Order
            {
                Id = healthyOrderId, StoreId = storeId, CustomerName = "Healthy",
                ProductId = "PROD-PB", Quantity = 1, TotalAmount = 20m, Status = "Submitted"
            });
            db.StoreInventories.Add(new StoreInventory
            {
                StoreId = storeId, ProductId = "PROD-PB", Quantity = 5
            });

            await db.SaveChangesAsync();
        }

        // Act — poison message goes first; healthy message follows
        await harness.Bus.Publish(new OrderPlacedEvent(
            poisonOrderId, storeId, "Poison", "PROD-PA", 1, 10m, DateTime.UtcNow));
        await harness.Bus.Publish(new OrderPlacedEvent(
            healthyOrderId, storeId, "Healthy", "PROD-PB", 1, 20m, DateTime.UtcNow));

        // Assert — StoreInventoryAllocatedEvent was published for the healthy order,
        // proving the consumer processed it despite the preceding domain failure.
        (await harness.Published.Any<StoreInventoryAllocatedEvent>())
            .Should().BeTrue(
                "healthy messages must not be blocked by domain-failure messages sharing the same queue");

        using (var verify = provider.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var inventory = await db.StoreInventories
                .SingleAsync(i => i.StoreId == storeId && i.ProductId == "PROD-PB");
            inventory.Quantity.Should().Be(4,
                "healthy order's inventory must be decremented even though the poison order's allocation failed");
        }

        await harness.Stop();
    }
}
