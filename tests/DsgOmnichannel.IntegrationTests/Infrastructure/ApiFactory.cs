using DsgOmnichannel.Api.Consumers;
using DsgOmnichannel.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;

namespace DsgOmnichannel.IntegrationTests.Infrastructure;

/// <summary>
/// WebApplicationFactory that replaces infrastructure dependencies with test doubles:
/// - SQL Server → ephemeral Testcontainer
/// - RabbitMQ bus → MassTransit in-memory test harness
/// Used for HTTP-level integration tests (e.g., order submission, outbox verification).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString => _sqlContainer.GetConnectionString();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        // Create the schema using EF Core (no migrations in this project)
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // ── Replace DB connection string with Testcontainer ──────────────
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(_sqlContainer.GetConnectionString()));

            // ── Replace RabbitMQ bus with MassTransit in-memory test harness ─
            // MassTransit registers many inter-dependent services; remove every descriptor
            // whose service type or implementation type lives in the MassTransit namespace
            // before calling AddMassTransitTestHarness so there are no duplicate keys.
            var massTransitDescriptors = services
                .Where(d =>
                    d.ServiceType.Namespace?.StartsWith("MassTransit") == true ||
                    d.ImplementationType?.Namespace?.StartsWith("MassTransit") == true)
                .ToList();
            foreach (var d in massTransitDescriptors)
                services.Remove(d);

            services.AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<OrderStatusNotificationConsumer>();

                // Restore the EF Core outbox so IPublishEndpoint.Publish() stages messages
                // in dbo.OutboxMessage instead of sending them directly to the in-memory bus.
                // This is what test 1a asserts: atomic commit of Order + OutboxMessage.
                x.AddEntityFrameworkOutbox<ApplicationDbContext>(opts =>
                {
                    opts.UseSqlServer();
                    opts.UseBusOutbox();
                });
            });

            // ── Deduplicate health check registrations ───────────────────────
            // MassTransit re-registers "masstransit-bus" via the test harness;
            // remove any duplicates introduced by the original health check setup.
            services.PostConfigure<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>(opts =>
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var duplicates = opts.Registrations
                    .Reverse()
                    .Where(r => !seen.Add(r.Name))
                    .ToList();
                foreach (var d in duplicates)
                    opts.Registrations.Remove(d);
            });
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _sqlContainer.StopAsync();
        await base.DisposeAsync();
    }
}
