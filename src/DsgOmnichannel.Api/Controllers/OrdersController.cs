using System.ComponentModel.DataAnnotations;
using DsgOmnichannel.Contracts.Events;
using DsgOmnichannel.Domain.Entities;
using DsgOmnichannel.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace DsgOmnichannel.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController(ApplicationDbContext dbContext, IPublishEndpoint publishEndpoint) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
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

        return Created($"/api/orders/{order.Id}", order);
    }
}

public class CreateOrderRequest
{
    [Required]
    [StringLength(50)]
    public string StoreId { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string ProductId { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal TotalAmount { get; set; }
}
