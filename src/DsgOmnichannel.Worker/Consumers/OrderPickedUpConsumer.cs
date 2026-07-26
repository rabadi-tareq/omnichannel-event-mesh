using DsgOmnichannel.Contracts.Events;
using DsgOmnichannel.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace DsgOmnichannel.Worker.Consumers;

public class OrderPickedUpConsumer(ApplicationDbContext dbContext) : IConsumer<OrderPickedUpEvent>
{
    public async Task Consume(ConsumeContext<OrderPickedUpEvent> context)
    {
        var order = await dbContext.Orders.FindAsync([context.Message.OrderId], context.CancellationToken);
        if (order is null)
            return;

        order.Status = "PickedUp";
        await dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
