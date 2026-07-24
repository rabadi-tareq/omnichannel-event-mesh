using DsgOmnichannel.Contracts.Events;
using DsgOmnichannel.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DsgOmnichannel.Worker.Consumers;

/// <summary>
/// Consumes OrderPlacedEvent and allocates inventory from store inventory if available.
/// Uses MassTransit's EF Core Inbox pattern for idempotent message processing.
/// </summary>
public class OrderPlacedEventConsumer : IConsumer<OrderPlacedEvent>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<OrderPlacedEventConsumer> _logger;

    public OrderPlacedEventConsumer(ApplicationDbContext dbContext, ILogger<OrderPlacedEventConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            ">>> [OrderPlacedEventConsumer] Processing OrderPlacedEvent: OrderId={OrderId}, StoreId={StoreId}, ProductId={ProductId}, Quantity={Quantity}",
            message.OrderId, message.StoreId, message.ProductId, message.Quantity);

        try
        {
            var order = await _dbContext.Orders
                .FirstOrDefaultAsync(o => o.Id == message.OrderId, context.CancellationToken)
                .ConfigureAwait(false);

            if (order == null)
            {
                _logger.LogWarning(
                    ">>> [OrderPlacedEventConsumer] Order not found for OrderId={OrderId}",
                    message.OrderId);
                return;
            }

            var storeInventory = await _dbContext.StoreInventories
                .FirstOrDefaultAsync(si =>
                    si.StoreId == message.StoreId &&
                    si.ProductId == message.ProductId, context.CancellationToken)
                .ConfigureAwait(false);

            if (storeInventory == null || storeInventory.Quantity < message.Quantity)
            {
                var reason = storeInventory == null
                    ? "Store inventory record not found"
                    : $"Insufficient inventory. Required: {message.Quantity}, Available: {storeInventory.Quantity}";

                _logger.LogWarning(
                    ">>> [OrderPlacedEventConsumer] Allocation failed for OrderId={OrderId}. Reason: {Reason}",
                    message.OrderId, reason);

                order.Status = "AllocationFailed";

                await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

                await context.Publish(new AllocationFailedEvent(
                    message.OrderId,
                    message.StoreId,
                    message.ProductId,
                    reason,
                    DateTime.UtcNow)).ConfigureAwait(false);

                return;
            }

            storeInventory.Quantity -= message.Quantity;
            order.Status = "Allocated";

            await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                ">>> [OrderPlacedEventConsumer] Inventory allocated successfully. StoreId={StoreId}, ProductId={ProductId}, QuantityAllocated={Quantity}, RemainingQuantity={Remaining}",
                message.StoreId, message.ProductId, message.Quantity, storeInventory.Quantity);

            await context.Publish(new StoreInventoryAllocatedEvent(
                message.OrderId,
                message.StoreId,
                message.ProductId,
                message.Quantity,
                DateTime.UtcNow)).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex,
                ">>> [OrderPlacedEventConsumer] Database error while processing OrderId={OrderId}",
                message.OrderId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                ">>> [OrderPlacedEventConsumer] Unexpected error while processing OrderId={OrderId}",
                message.OrderId);
            throw;
        }
    }
}
