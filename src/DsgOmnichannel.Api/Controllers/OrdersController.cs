using System.ComponentModel.DataAnnotations;
using DsgOmnichannel.Contracts.Events;
using DsgOmnichannel.Domain.Entities;
using DsgOmnichannel.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace DsgOmnichannel.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController(ApplicationDbContext dbContext, IPublishEndpoint publishEndpoint) : ControllerBase
{
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    [HttpPost]
    public async Task<IActionResult> PlaceOrderAsync([FromBody] PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var order = new Order
        {
            CustomerName = request.CustomerName,
            TotalAmount = request.TotalAmount,
        };

        _dbContext.Orders.Add(order);

        var orderPlacedEvent = new OrderPlacedEvent(order.Id, order.CustomerName, order.SKU, order.Quantity, order.TotalAmount, order.CreatedAtUtc);
        await _publishEndpoint.Publish(orderPlacedEvent, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/api/orders/{order.Id}", new { order.Id, order.CustomerName, order.TotalAmount, order.CreatedAtUtc });
    }
}

public class PlaceOrderRequest
{
    [Required]
    [StringLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal TotalAmount { get; set; }
}
