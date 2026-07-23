using DsgOmnichannel.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Options;

namespace DsgOmnichannel.Api.Messaging;

internal static class MassTransitServiceCollectionExtensions
{
    internal static IServiceCollection AddApiMassTransit(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<ApplicationDbContext>(options =>
            {
                options.UseSqlServer();
                options.UseBusOutbox();
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitMqOptions = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

                cfg.Host(rabbitMqOptions.Host, rabbitMqOptions.VirtualHost, h =>
                {
                    h.Username(rabbitMqOptions.Username);
                    h.Password(rabbitMqOptions.Password);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
