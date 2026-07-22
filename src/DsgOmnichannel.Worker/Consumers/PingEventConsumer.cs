using DsgOmnichannel.Domain.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace DsgOmnichannel.Infrastructure.Messaging;

public class PingEventConsumer : IConsumer<PingEvent>
{
    private readonly ILogger<PingEventConsumer> _logger;

    public PingEventConsumer(ILogger<PingEventConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<PingEvent> context)
    {
        _logger.LogInformation(">>> [MassTransit Consumer] Received PingEvent: {Message} (ID: {Id})",
            context.Message.Message, context.Message.Id);

        return Task.CompletedTask;
    }
}