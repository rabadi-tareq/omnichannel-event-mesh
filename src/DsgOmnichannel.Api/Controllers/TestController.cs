using DsgOmnichannel.Contracts.Events;
using DsgOmnichannel.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DsgOmnichannel.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController(IPublishEndpoint publishEndpoint, ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet("public")]
    [AllowAnonymous]
    public IActionResult Public()
    {
        return Ok("Public endpoint accessible");
    }

    [HttpGet("secured")]
    [Authorize(Policy = "RequireCustomerRole")]
    public IActionResult Secured()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
        return Ok(new { Message = "Secured endpoint accessed", Claims = claims });
    }

    [HttpPost("publish-order-event")]
    [AllowAnonymous]
    public async Task<IActionResult> PublishOrderEvent(
        [FromBody] PublishOrderEventTestRequest request,
        CancellationToken cancellationToken)
    {
        await publishEndpoint.Publish<OrderPlacedEvent>(
            new OrderPlacedEvent(
                request.OrderId,
                request.StoreId,
                request.CustomerName ?? "Test Customer",
                request.ProductId,
                request.Quantity,
                request.TotalAmount > 0 ? request.TotalAmount : 100.00m,
                DateTime.UtcNow),
            context =>
            {
                context.MessageId = request.MessageId;
            },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { Message = "Event published successfully", request.MessageId, request.OrderId });
    }
}

public class PublishOrderEventTestRequest
{
    public Guid MessageId { get; set; }
    public Guid OrderId { get; set; }
    public string StoreId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal TotalAmount { get; set; }
}
