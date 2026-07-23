using DsgOmnichannel.Api.Configuration;
using DsgOmnichannel.Api.HealthChecks;
using DsgOmnichannel.Api.Messaging;
using DsgOmnichannel.Api.Security;
using DsgOmnichannel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DsgOmnichannel.Api.Startup;

internal static class ApiServiceCollectionExtensions
{
    internal static IServiceCollection AddApiCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }

    internal static IServiceCollection AddApiInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var databaseConnectionString = configuration.GetRequiredDatabaseConnectionString();

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(databaseConnectionString));
        services.AddApiHealthChecks(configuration);

        return services;
    }

    internal static IServiceCollection AddApiMessaging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApiMassTransit();

        return services;
    }

    internal static IServiceCollection AddApiSecurity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApiAuthenticationAndAuthorization();

        return services;
    }

    internal static IServiceCollection AddApiConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddApiOptions(configuration);

        return services;
    }
}
