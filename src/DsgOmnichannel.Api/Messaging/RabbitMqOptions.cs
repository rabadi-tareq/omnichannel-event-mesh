namespace DsgOmnichannel.Api.Messaging;

internal sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public string Host { get; set; } = "localhost";

    public string VirtualHost { get; set; } = "/";

    public string Username { get; set; } = "guest";

    public string Password { get; set; } = "guest";
}
