using DsgOmnichannel.Contracts.Events;
using DsgOmnichannel.Domain.Entities;
using DsgOmnichannel.Infrastructure.Persistence;
using MassTransit;

namespace DsgOmnichannel.Worker.Consumers;

public class OrderStatusHistoryConsumer :
    IConsumer<OrderPlacedEvent>,
    IConsumer<StoreInventoryAllocatedEvent>,
    IConsumer<AllocationFailedEvent>,
    IConsumer<OrderPickedUpEvent>
{
    private readonly ApplicationDbContext _dbContext;

    public OrderStatusHistoryConsumer(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        _dbContext.OrderStatusHistories.Add(new OrderStatusHistory
        {
            Id = NewId.NextGuid(),
            OrderId = context.Message.OrderId,
            Status = "Submitted",
            Reason = "Order received via API",
            CreatedAtUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }

    public async Task Consume(ConsumeContext<StoreInventoryAllocatedEvent> context)
    {
        _dbContext.OrderStatusHistories.Add(new OrderStatusHistory
        {
            Id = NewId.NextGuid(),
            OrderId = context.Message.OrderId,
            Status = "Allocated",
            Reason = "Inventory successfully reserved",
            CreatedAtUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }

    public async Task Consume(ConsumeContext<AllocationFailedEvent> context)
    {
        _dbContext.OrderStatusHistories.Add(new OrderStatusHistory
        {
            Id = NewId.NextGuid(),
            OrderId = context.Message.OrderId,
            Status = "AllocationFailed",
            Reason = context.Message.Reason,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }

    public async Task Consume(ConsumeContext<OrderPickedUpEvent> context)
    {
        _dbContext.OrderStatusHistories.Add(new OrderStatusHistory
        {
            Id = NewId.NextGuid(),
            OrderId = context.Message.OrderId,
            Status = "PickedUp",
            Reason = $"Confirmed by associate {context.Message.AssociateId}",
            CreatedAtUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }
}
