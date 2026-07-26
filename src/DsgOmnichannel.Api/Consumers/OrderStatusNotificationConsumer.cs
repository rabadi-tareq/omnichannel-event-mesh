using DsgOmnichannel.Api.Hubs;
using DsgOmnichannel.Contracts.Events;
using DsgOmnichannel.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DsgOmnichannel.Api.Consumers;

public class OrderStatusNotificationConsumer(
    IHubContext<OrderHub> hubContext,
    ApplicationDbContext dbContext) :
    IConsumer<OrderPlacedEvent>,
    IConsumer<StoreInventoryAllocatedEvent>,
    IConsumer<AllocationFailedEvent>,
    IConsumer<OrderPickedUpEvent>,
    IConsumer<OrderCancelledEvent>
{
    public async Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        var msg = context.Message;
        var displayOrderId = $"{msg.Quantity} Count of {msg.ProductId}";

        await hubContext.Clients.All.SendAsync("ReceiveOrderJourneyEvent", new
        {
            displayOrderId,
            components = new[] { "MassTransit Outbox Relay", "RabbitMQ", "API MassTransit Consumer" },
            eventName = "MessageDelivered",
            messages = new[]
            {
                "MassTransit outbox relay picked up the outbox message and published it to RabbitMQ.",
                "RabbitMQ delivered OrderPlacedEvent to all subscribers.",
                "API MassTransit Consumer received OrderPlacedEvent — outbox-to-broker round-trip confirmed."
            },
            timestamp = DateTime.UtcNow.ToString("O")
        });
    }

    public async Task Consume(ConsumeContext<StoreInventoryAllocatedEvent> context)
    {
        var msg = context.Message;
        var displayOrderId = $"{msg.Quantity} Count of {msg.ProductId}";

        await hubContext.Clients.All.SendAsync("ReceiveOrderJourneyEvent", new
        {
            displayOrderId,
            components = new[] { "RabbitMQ", "API MassTransit Consumer" },
            eventName = "AllocationConfirmed",
            messages = new[]
            {
                "API MassTransit Consumer received StoreInventoryAllocatedEvent from RabbitMQ.",
                $"Order is ready for customer pickup at store {msg.StoreId}."
            },
            timestamp = DateTime.UtcNow.ToString("O")
        });
    }

    public async Task Consume(ConsumeContext<AllocationFailedEvent> context)
    {
        var msg = context.Message;
        var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == msg.OrderId);
        var displayOrderId = order is not null
            ? $"{order.Quantity} Count of {msg.ProductId}"
            : $"Order for {msg.ProductId}";

        await hubContext.Clients.All.SendAsync("ReceiveOrderJourneyEvent", new
        {
            displayOrderId,
            components = new[] { "RabbitMQ", "API MassTransit Consumer" },
            eventName = "AllocationFailed",
            messages = new[]
            {
                "API MassTransit Consumer received AllocationFailedEvent from RabbitMQ.",
                $"Reason: {msg.Reason}"
            },
            timestamp = DateTime.UtcNow.ToString("O")
        });
    }

    public async Task Consume(ConsumeContext<OrderPickedUpEvent> context)
    {
        var msg = context.Message;
        var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == msg.OrderId);
        var displayOrderId = order is not null
            ? $"{order.Quantity} Count of {order.ProductId}"
            : $"Order {msg.OrderId}";

        await hubContext.Clients.All.SendAsync("ReceiveOrderJourneyEvent", new
        {
            displayOrderId,
            components = new[] { "RabbitMQ", "API MassTransit Consumer" },
            eventName = "OrderPickedUp",
            messages = new[]
            {
                "API MassTransit Consumer received OrderPickedUpEvent from RabbitMQ.",
                $"Order confirmed picked up by associate '{msg.AssociateId}' at store {msg.StoreId}."
            },
            timestamp = DateTime.UtcNow.ToString("O")
        });
    }

    public async Task Consume(ConsumeContext<OrderCancelledEvent> context)
    {
        var msg = context.Message;
        var displayOrderId = $"{msg.Quantity} Count of {msg.ProductId}";

        await hubContext.Clients.All.SendAsync("ReceiveOrderJourneyEvent", new
        {
            displayOrderId,
            components = new[] { "MassTransit Outbox Relay", "RabbitMQ", "API MassTransit Consumer" },
            eventName = "CancellationEventDelivered",
            messages = new[]
            {
                "MassTransit outbox relay forwarded OrderCancelledEvent to RabbitMQ.",
                "RabbitMQ delivered OrderCancelledEvent to all subscribers.",
                "API MassTransit Consumer confirmed — Worker will now process the cancellation."
            },
            timestamp = DateTime.UtcNow.ToString("O")
        });
    }
}
