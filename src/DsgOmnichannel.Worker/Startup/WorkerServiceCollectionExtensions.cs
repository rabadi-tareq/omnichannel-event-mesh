using DsgOmnichannel.Infrastructure.Persistence;
using DsgOmnichannel.Worker.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace DsgOmnichannel.Worker.Startup;

/// <summary>
/// Extension methods for configuring Worker services, including database and MassTransit messaging.
/// Keeps Program.cs clean and concise by centralizing all service registration logic.
/// </summary>
internal static class WorkerServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ApplicationDbContext with SQL Server.
    /// </summary>
    internal static IServiceCollection AddWorkerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var databaseConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not found in configuration");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(databaseConnectionString));

        return services;
    }

    /// <summary>
    /// Configures MassTransit with RabbitMQ transport and EF Core Inbox/Outbox patterns.
    /// Enables consumer idempotency via InboxState tracking and transactional outbox for outgoing events.
    /// </summary>
    internal static IServiceCollection AddWorkerMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            // Register consumers
            x.AddConsumer<OrderPlacedEventConsumer>();
            x.AddConsumer<PingEventConsumer>();

            // Configure EF Core outbox and inbox persistence
            x.AddEntityFrameworkOutbox<ApplicationDbContext>(options =>
            {
                options.UseSqlServer();
            });

            x.AddConfigureEndpointsCallback((context, name, cfg) =>
            {
                cfg.UseEntityFrameworkOutbox<ApplicationDbContext>(context);
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitHost = configuration["RabbitMQ:Host"] ?? "localhost";
                var rabbitUser = configuration["RabbitMQ:Username"] ?? "guest";
                var rabbitPass = configuration["RabbitMQ:Password"] ?? "guest";

                cfg.Host(rabbitHost, "/", h =>
                {
                    h.Username(rabbitUser);
                    h.Password(rabbitPass);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
