using DsgOmnichannel.Contracts.Events;
using DsgOmnichannel.Infrastructure.Persistence;
using DsgOmnichannel.Worker.Services;
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
    private readonly WorkerSignalRService _signalR;

    public OrderPlacedEventConsumer(
        ApplicationDbContext dbContext,
        ILogger<OrderPlacedEventConsumer> logger,
        WorkerSignalRService signalR)
    {
        _dbContext = dbContext;
        _logger = logger;
        _signalR = signalR;
    }

    public async Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        var message = context.Message;
        var displayOrderId = $"{message.Quantity} Count of {message.ProductId}";

        _logger.LogInformation(
            ">>> [OrderPlacedEventConsumer] Processing OrderPlacedEvent: OrderId={OrderId}, StoreId={StoreId}, ProductId={ProductId}, Quantity={Quantity}",
            message.OrderId, message.StoreId, message.ProductId, message.Quantity);

        await _signalR.NotifyAsync(
            displayOrderId,
            ["RabbitMQ", "Worker MassTransit Consumer"],
            "WorkerReceivedOrder",
            [
                "Worker MassTransit Consumer dequeued OrderPlacedEvent from RabbitMQ.",
                $"Beginning inventory allocation for {message.Quantity} unit(s) of '{message.ProductId}' at store {message.StoreId}."
            ],
            context.CancellationToken);

        try
        {
            var order = await _dbContext.Orders
                .FirstOrDefaultAsync(o => o.Id == message.OrderId, context.CancellationToken)
                .ConfigureAwait(false);

            if (order == null)
            {
                _logger.LogWarning(">>> [OrderPlacedEventConsumer] Order not found for OrderId={OrderId}", message.OrderId);
                return;
            }

            var storeInventory = await _dbContext.StoreInventories
                .FirstOrDefaultAsync(
                    si => si.StoreId == message.StoreId && si.ProductId == message.ProductId,
                    context.CancellationToken)
                .ConfigureAwait(false);

            if (storeInventory == null || storeInventory.Quantity < message.Quantity)
            {
                var reason = storeInventory == null
                    ? $"Inventory record for product '{message.ProductId}' does not exist at store '{message.StoreId}'."
                    : $"Insufficient stock for product '{message.ProductId}' at store '{message.StoreId}'. Requested: {message.Quantity}, Available: {storeInventory.Quantity}.";

                _logger.LogWarning(">>> [OrderPlacedEventConsumer] Allocation failed for OrderId={OrderId}. Reason: {Reason}", message.OrderId, reason);

                order.Status = "AllocationFailed";
                await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

                await _signalR.NotifyAsync(
                    displayOrderId,
                    ["Worker MassTransit Consumer", "EF Core", "SQL Server", "RabbitMQ"],
                    "InventoryAllocationFailed",
                    [
                        $"Inventory check failed — {reason}",
                        "Order status updated to AllocationFailed in SQL Server via EF Core.",
                        "AllocationFailedEvent published to RabbitMQ."
                    ],
                    context.CancellationToken);

                await context.Publish(
                    new AllocationFailedEvent(message.OrderId, message.StoreId, message.ProductId, reason, DateTime.UtcNow))
                    .ConfigureAwait(false);

                return;
            }

            storeInventory.Quantity -= message.Quantity;
            order.Status = "Allocated";
            await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                ">>> [OrderPlacedEventConsumer] Inventory allocated successfully. StoreId={StoreId}, ProductId={ProductId}, QuantityAllocated={Quantity}, RemainingQuantity={Remaining}",
                message.StoreId, message.ProductId, message.Quantity, storeInventory.Quantity);

            await _signalR.NotifyAsync(
                displayOrderId,
                ["Worker MassTransit Consumer", "EF Core", "SQL Server", "RabbitMQ"],
                "InventoryAllocated",
                [
                    $"Inventory check passed — {message.Quantity} unit(s) of '{message.ProductId}' available at store {message.StoreId}.",
                    $"EF Core decremented stock and committed to SQL Server. Remaining stock: {storeInventory.Quantity} unit(s).",
                    "Order status updated to Allocated.",
                    "StoreInventoryAllocatedEvent published to RabbitMQ."
                ],
                context.CancellationToken);

            await context.Publish(
                new StoreInventoryAllocatedEvent(message.OrderId, message.StoreId, message.ProductId, message.Quantity, DateTime.UtcNow))
                .ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, ">>> [OrderPlacedEventConsumer] Database error while processing OrderId={OrderId}", message.OrderId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ">>> [OrderPlacedEventConsumer] Unexpected error while processing OrderId={OrderId}", message.OrderId);
            throw;
        }
    }
}
