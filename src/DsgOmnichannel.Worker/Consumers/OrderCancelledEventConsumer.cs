using DsgOmnichannel.Contracts.Events;
using DsgOmnichannel.Infrastructure.Persistence;
using DsgOmnichannel.Worker.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DsgOmnichannel.Worker.Consumers;

/// <summary>
/// Handles order cancellations by restoring the previously allocated inventory back to stock.
/// </summary>
public class OrderCancelledEventConsumer : IConsumer<OrderCancelledEvent>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<OrderCancelledEventConsumer> _logger;
    private readonly WorkerSignalRService _signalR;

    public OrderCancelledEventConsumer(
        ApplicationDbContext dbContext,
        ILogger<OrderCancelledEventConsumer> logger,
        WorkerSignalRService signalR)
    {
        _dbContext = dbContext;
        _logger = logger;
        _signalR = signalR;
    }

    public async Task Consume(ConsumeContext<OrderCancelledEvent> context)
    {
        var message = context.Message;
        var displayOrderId = $"{message.Quantity} Count of {message.ProductId}";

        _logger.LogInformation(
            ">>> [OrderCancelledEventConsumer] Processing cancellation: OrderId={OrderId}, ProductId={ProductId}, Quantity={Quantity}",
            message.OrderId, message.ProductId, message.Quantity);

        await _signalR.NotifyAsync(
            displayOrderId,
            ["RabbitMQ", "Worker MassTransit Consumer"],
            "CancellationReceivedByWorker",
            [
                "Worker MassTransit Consumer dequeued OrderCancelledEvent from RabbitMQ.",
                $"Preparing to restore {message.Quantity} unit(s) of '{message.ProductId}' to stock at store {message.StoreId}."
            ],
            context.CancellationToken);

        try
        {
            var order = await _dbContext.Orders
                .FirstOrDefaultAsync(o => o.Id == message.OrderId, context.CancellationToken)
                .ConfigureAwait(false);

            if (order is null)
            {
                _logger.LogWarning(">>> [OrderCancelledEventConsumer] Order not found: OrderId={OrderId}", message.OrderId);
                return;
            }

            var storeInventory = await _dbContext.StoreInventories
                .FirstOrDefaultAsync(
                    si => si.StoreId == message.StoreId && si.ProductId == message.ProductId,
                    context.CancellationToken)
                .ConfigureAwait(false);

            if (storeInventory is null)
            {
                _logger.LogWarning(
                    ">>> [OrderCancelledEventConsumer] Inventory record not found for ProductId={ProductId}, StoreId={StoreId}",
                    message.ProductId, message.StoreId);

                await _signalR.NotifyAsync(
                    displayOrderId,
                    ["Worker MassTransit Consumer", "EF Core", "SQL Server"],
                    "CancellationWarning",
                    [
                        $"Inventory record for '{message.ProductId}' at store {message.StoreId} not found — stock could not be restored.",
                        "Order status updated to Cancelled."
                    ],
                    context.CancellationToken);

                order.Status = "Cancelled";
                await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
                return;
            }

            storeInventory.Quantity += message.Quantity;
            order.Status = "Cancelled";

            await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                ">>> [OrderCancelledEventConsumer] Stock restored. ProductId={ProductId}, RestoredQty={Qty}, NewTotal={Total}",
                message.ProductId, message.Quantity, storeInventory.Quantity);

            await _signalR.NotifyAsync(
                displayOrderId,
                ["Worker MassTransit Consumer", "EF Core", "SQL Server"],
                "InventoryRestored",
                [
                    $"{message.Quantity} unit(s) of '{message.ProductId}' returned to stock at store {message.StoreId}.",
                    $"New stock level: {storeInventory.Quantity} unit(s).",
                    "Order status updated to Cancelled in SQL Server via EF Core."
                ],
                context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                ">>> [OrderCancelledEventConsumer] Error processing cancellation for OrderId={OrderId}", message.OrderId);
            throw;
        }
    }
}
