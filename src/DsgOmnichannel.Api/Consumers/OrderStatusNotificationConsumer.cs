using DsgOmnichannel.Api.Hubs;
using DsgOmnichannel.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace DsgOmnichannel.Api.Consumers;

public class OrderStatusNotificationConsumer :
    IConsumer<OrderPlacedEvent>,
    IConsumer<StoreInventoryAllocatedEvent>,
    IConsumer<AllocationFailedEvent>
{
    private readonly IHubContext<OrderHub> _hubContext;

    public OrderStatusNotificationConsumer(IHubContext<OrderHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveOrderUpdate", new
        {
            orderId = context.Message.OrderId,
            status = "Submitted",
            timestamp = DateTime.UtcNow
        });
    }

    public async Task Consume(ConsumeContext<StoreInventoryAllocatedEvent> context)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveOrderUpdate", new
        {
            orderId = context.Message.OrderId,
            status = "ReadyForPickup",
            timestamp = DateTime.UtcNow
        });
    }

    public async Task Consume(ConsumeContext<AllocationFailedEvent> context)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveOrderUpdate", new
        {
            orderId = context.Message.OrderId,
            status = "AllocationFailed",
            timestamp = DateTime.UtcNow
        });
    }
}
