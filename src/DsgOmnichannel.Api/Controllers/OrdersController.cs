using System.ComponentModel.DataAnnotations;
using DsgOmnichannel.Api.Hubs;
using DsgOmnichannel.Contracts.Events;
using DsgOmnichannel.Domain.Entities;
using DsgOmnichannel.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace DsgOmnichannel.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController(
    ApplicationDbContext dbContext,
    IPublishEndpoint publishEndpoint,
    IHubContext<OrderHub> hubContext) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var displayOrderId = $"{request.Quantity} Count of {request.ProductId}";

        await hubContext.Clients.All.SendAsync("ReceiveOrderJourneyEvent", new
        {
            displayOrderId,
            components = new[] { "API" },
            eventName = "OrderReceived",
            messages = new[]
            {
                $"POST /api/orders received — building order for {request.Quantity} Count of '{request.ProductId}' at store {request.StoreId}."
            },
            timestamp = DateTime.UtcNow.ToString("O")
        }, cancellationToken);

        var order = new Order
        {
            CustomerName = request.CustomerName,
            StoreId = request.StoreId,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            TotalAmount = request.TotalAmount,
            Status = "Submitted",
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Orders.Add(order);

        await publishEndpoint.Publish(
            new OrderPlacedEvent(order.Id, request.StoreId, order.CustomerName, order.ProductId, order.Quantity, order.TotalAmount, order.CreatedAt),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await hubContext.Clients.All.SendAsync("ReceiveOrderJourneyEvent", new
        {
            displayOrderId,
            components = new[] { "EF Core", "SQL Server", "MassTransit Outbox" },
            eventName = "OrderPersisted",
            messages = new[]
            {
                "EF Core committed order row to the Orders table in SQL Server.",
                "EF Core committed OrderPlacedEvent to the MassTransit outbox table in the same transaction — both rows committed atomically.",
                "MassTransit outbox relay will poll and forward this message to RabbitMQ."
            },
            timestamp = DateTime.UtcNow.ToString("O")
        }, cancellationToken);

        return Created($"/api/orders/{order.Id}", order);
    }

    [HttpPost("{id:guid}/pickup")]
    public async Task<IActionResult> ConfirmPickup(Guid id, [FromBody] ConfirmPickupRequest request, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.FindAsync([id], cancellationToken);
        if (order is null)
            return NotFound();

        if (order.Status != "Allocated")
            return Conflict(new { error = $"Order is not in a pickable state. Current status: {order.Status}" });

        await publishEndpoint.Publish(
            new OrderPickedUpEvent(order.Id, order.StoreId, request.AssociateId, DateTime.UtcNow),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { orderId = order.Id, status = "PickedUp" });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid id, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.FindAsync([id], cancellationToken);
        if (order is null)
            return NotFound();

        if (order.Status != "Allocated")
            return Conflict(new { error = $"Only allocated orders can be cancelled. Current status: {order.Status}" });

        var displayOrderId = $"{order.Quantity} Count of {order.ProductId}";

        await hubContext.Clients.All.SendAsync("ReceiveOrderJourneyEvent", new
        {
            displayOrderId,
            components = new[] { "API" },
            eventName = "OrderCancellationRequested",
            messages = new[]
            {
                $"POST /api/orders/{id}/cancel received.",
                $"Publishing OrderCancelledEvent — Worker will restore {order.Quantity} unit(s) of '{order.ProductId}' to stock."
            },
            timestamp = DateTime.UtcNow.ToString("O")
        }, cancellationToken);

        await publishEndpoint.Publish(
            new OrderCancelledEvent(order.Id, order.StoreId, order.ProductId, order.Quantity, DateTime.UtcNow),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { orderId = order.Id, status = "CancellationRequested" });
    }
}

public class ConfirmPickupRequest
{
    [Required]
    [StringLength(100)]
    public string AssociateId { get; set; } = string.Empty;
}

public class CreateOrderRequest
{
    [Required]
    [StringLength(50)]
    public string StoreId { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string ProductId { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal TotalAmount { get; set; }
}
