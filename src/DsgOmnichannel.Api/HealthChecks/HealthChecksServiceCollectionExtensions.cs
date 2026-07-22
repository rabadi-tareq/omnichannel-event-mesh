using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace DsgOmnichannel.Api.HealthChecks;

internal static class HealthChecksServiceCollectionExtensions
{
    internal static IServiceCollection AddApiHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var databaseConnectionString = configuration.GetRequiredMasterDatabaseConnectionString();
        var rabbitMqConnectionString = configuration.GetRabbitMqConnectionString();

        services.AddSingleton<IConnection>(_ =>
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(rabbitMqConnectionString),
            };

            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

        services.AddHealthChecks()
            .AddSqlServer(databaseConnectionString, name: "sqlserver", tags: ["db"])
            .AddRabbitMQ(sp => sp.GetRequiredService<IConnection>(), name: "rabbitmq", tags: ["messaging"]);

        return services;
    }

    internal static string GetRequiredDatabaseConnectionString(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var databaseConnectionString = configuration.GetConnectionString("Database")
            ?? configuration.GetConnectionString("DefaultConnection");

        ArgumentException.ThrowIfNullOrWhiteSpace(databaseConnectionString);

        return databaseConnectionString;
    }

    private static string GetRequiredMasterDatabaseConnectionString(this IConfiguration configuration)
    {
        var databaseConnectionString = configuration.GetRequiredDatabaseConnectionString();

        var connectionStringBuilder = new SqlConnectionStringBuilder(databaseConnectionString)
        {
            InitialCatalog = "master",
        };

        return connectionStringBuilder.ConnectionString;
    }

    private static string GetRabbitMqConnectionString(this IConfiguration configuration)
    {
        var rabbitMqConnectionString = configuration.GetConnectionString("RabbitMQ");
        if (!string.IsNullOrWhiteSpace(rabbitMqConnectionString))
        {
            return rabbitMqConnectionString;
        }

        var rabbitMqHost = configuration["RabbitMQ:Host"] ?? "localhost";
        var rabbitMqUsername = configuration["RabbitMQ:Username"] ?? "guest";
        var rabbitMqPassword = configuration["RabbitMQ:Password"] ?? "guest";
        var rabbitMqVirtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/";

        if (!rabbitMqVirtualHost.StartsWith('/'))
        {
            rabbitMqVirtualHost = $"/{rabbitMqVirtualHost}";
        }

        return $"amqp://{rabbitMqUsername}:{rabbitMqPassword}@{rabbitMqHost}{rabbitMqVirtualHost}";
    }
}
