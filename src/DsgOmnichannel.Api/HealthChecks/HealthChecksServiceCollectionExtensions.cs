using DsgOmnichannel.Api.Messaging;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DsgOmnichannel.Api.HealthChecks;

internal static class HealthChecksServiceCollectionExtensions
{
    internal static IServiceCollection AddApiHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var databaseConnectionString = configuration.GetRequiredMasterDatabaseConnectionString();

        services.AddSingleton<IConnection>(sp =>
        {
            var rabbitMqOptions = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            var rabbitMqVirtualHost = rabbitMqOptions.VirtualHost;

            if (!rabbitMqVirtualHost.StartsWith('/'))
            {
                rabbitMqVirtualHost = $"/{rabbitMqVirtualHost}";
            }

            var rabbitMqConnectionString = $"amqp://{rabbitMqOptions.Username}:{rabbitMqOptions.Password}@{rabbitMqOptions.Host}{rabbitMqVirtualHost}";

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
}
