using DsgOmnichannel.Domain.Events;
using MassTransit;

namespace DsgOmnichannel.Api.Endpoints;

internal static class TestPublishEndpointRouteBuilderExtensions
{
    internal static IEndpointRouteBuilder MapTestPublishEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("/test-publish", async (IPublishEndpoint publishEndpoint, string text) =>
        {
            var pingEvent = new PingEvent(Guid.NewGuid(), text, DateTime.UtcNow);
            await publishEndpoint.Publish(pingEvent);

            return Results.Ok(new { Status = "Published", Message = pingEvent });
        });

        return endpoints;
    }
}
