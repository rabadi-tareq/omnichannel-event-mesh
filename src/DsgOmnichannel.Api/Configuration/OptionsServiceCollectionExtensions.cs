using DsgOmnichannel.Api.Messaging;
using DsgOmnichannel.Api.Security;

namespace DsgOmnichannel.Api.Configuration;

internal static class OptionsServiceCollectionExtensions
{
    internal static IServiceCollection AddApiOptions(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer must not be empty.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience must not be empty.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.SigningKey), "Jwt:SigningKey must not be empty.")
            .ValidateOnStart();

        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.VirtualHost))
                {
                    options.VirtualHost = "/";
                }
                else if (!options.VirtualHost.StartsWith('/'))
                {
                    options.VirtualHost = $"/{options.VirtualHost}";
                }
            })
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "RabbitMQ:Host must not be empty.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Username), "RabbitMQ:Username must not be empty.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Password), "RabbitMQ:Password must not be empty.")
            .ValidateOnStart();

        return services;
    }
}
