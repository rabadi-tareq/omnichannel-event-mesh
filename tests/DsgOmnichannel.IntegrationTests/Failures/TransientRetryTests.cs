using DsgOmnichannel.Contracts.Events;
using DsgOmnichannel.Domain.Entities;
using DsgOmnichannel.IntegrationTests.Infrastructure;
using DsgOmnichannel.Worker.Consumers;
using Microsoft.EntityFrameworkCore;

namespace DsgOmnichannel.IntegrationTests.Failures;

/// <summary>
/// Failure 5 — Transient Dependency Failures
///
/// Verifies that the exponential backoff retry policy (~1s, ~3s, ~5s) absorbs transient
/// downstream failures (e.g., a DbUpdateException simulating a SQL Server blip) without
/// dropping the message. The consumer must eventually succeed after the transient fault
/// clears.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class TransientRetryTests(ApiFactory factory)
{
    [Fact]
    public async Task OrderPlacedConsumer_WhenTransientDbFaultOccurs_MessageRetriedAndEventuallyProcessed()
    {
        // Arrange — FaultInjector is a singleton shared across all DI scope boundaries.
        // MassTransit's retry middleware creates a new scope per attempt, so a per-instance
        // flag on FaultyApplicationDbContext would reset on every retry. The singleton
        // ensures the injector counts across the first attempt AND the retry attempt.
        var injector = new FaultInjector(failuresBeforeSuccess: 1);

        await using var provider = new ServiceCollection()
            .AddSingleton(injector)
            .AddScoped<ApplicationDbContext>(sp =>
            {
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlServer(factory.ConnectionString)
                    .Options;
                return new FaultyApplicationDbContext(options, sp.GetRequiredService<FaultInjector>());
            })
            .AddLogging()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<OrderPlacedEventConsumer>();

                // Mirror the Worker's retry policy — DbUpdateException is in the Handle<> list,
                // so the middleware retries up to 3 times before faulting.
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

        // Extend the harness timeout to accommodate the ~1s exponential backoff delay
        // before the first retry fires.
        harness.TestTimeout = TimeSpan.FromSeconds(20);

        await harness.Start();

        var orderId = Guid.NewGuid();
        const string storeId = "STORE-T01";
        const string productId = "PROD-T01";

        // Seed via a plain ApplicationDbContext constructed directly — bypasses the
        // FaultyApplicationDbContext DI registration so the fault injector is not
        // triggered during setup. Only the consumer's SaveChangesAsync will fault.
        var seedOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(factory.ConnectionString)
            .Options;

        await using (var seedDb = new ApplicationDbContext(seedOptions))
        {
            seedDb.Orders.Add(new Order
            {
                Id = orderId,
                StoreId = storeId,
                CustomerName = "Retry Test",
                ProductId = productId,
                Quantity = 1,
                TotalAmount = 50m,
                Status = "Submitted"
            });
            seedDb.StoreInventories.Add(new StoreInventory
            {
                StoreId = storeId,
                ProductId = productId,
                Quantity = 5
            });
            await seedDb.SaveChangesAsync();
        }

        // Act — first delivery faults (DbUpdateException on SaveChangesAsync),
        // retry attempt ~1s later succeeds.
        await harness.Bus.Publish(new OrderPlacedEvent(
            orderId, storeId, "Retry Test", productId, 1, 50m, DateTime.UtcNow));

        // Assert — StoreInventoryAllocatedEvent is only published on a SUCCESSFUL consumer
        // execution. Waiting for it implicitly proves the retry succeeded.
        (await harness.Published.Any<StoreInventoryAllocatedEvent>())
            .Should().BeTrue(
                "consumer must succeed on the retry attempt after a transient DbUpdateException " +
                "and publish StoreInventoryAllocatedEvent — the message must not be dropped");

        // Assert — inventory was decremented exactly once.
        // The faulted attempt threw before SaveChangesAsync committed, so no partial write occurred.
        using (var verify = provider.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var inventory = await db.StoreInventories
                .SingleAsync(i => i.StoreId == storeId && i.ProductId == productId);

            inventory.Quantity.Should().Be(4,
                "inventory must be decremented exactly once — the faulted attempt must not " +
                "have partially committed before the retry succeeded");
        }

        // Assert — the injector was triggered exactly once, proving retry occurred.
        injector.FaultCount.Should().Be(1,
            "DbUpdateException must have been injected exactly once before the retry cleared it");

        await harness.Stop();
    }
}

/// <summary>
/// Singleton fault injector shared across DI scope boundaries.
/// Throws DbUpdateException on each SaveChangesAsync call until the configured failure
/// count is reached, then allows all subsequent calls through.
/// </summary>
internal sealed class FaultInjector(int failuresBeforeSuccess = 1)
{
    private int _callCount;

    public int FaultCount { get; private set; }

    /// <summary>Resets counters so seeding calls do not count against the fault budget.</summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _callCount, 0);
        FaultCount = 0;
    }

    public void MaybeFault()
    {
        if (Interlocked.Increment(ref _callCount) <= failuresBeforeSuccess)
        {
            FaultCount++;
            throw new DbUpdateException(
                "Simulated transient SQL Server blip", new Exception("inner fault"));
        }
    }
}

/// <summary>
/// Test-double DbContext that delegates everything to ApplicationDbContext but intercepts
/// SaveChangesAsync and passes it through FaultInjector before the real commit.
/// Registered as ApplicationDbContext in the test harness so OrderPlacedEventConsumer
/// receives it transparently via constructor injection.
/// </summary>
internal sealed class FaultyApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    FaultInjector injector) : ApplicationDbContext(options)
{
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        injector.MaybeFault();
        return base.SaveChangesAsync(cancellationToken);
    }
}
