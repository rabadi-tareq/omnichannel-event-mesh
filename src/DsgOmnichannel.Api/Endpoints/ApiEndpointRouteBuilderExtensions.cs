using DsgOmnichannel.Api.HealthChecks;
using DsgOmnichannel.Api.Hubs;

namespace DsgOmnichannel.Api.Endpoints;

internal static class ApiEndpointRouteBuilderExtensions
{
    internal static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapControllers();
        endpoints.MapGet("/", () => Results.Redirect("/swagger"));
        endpoints.MapHub<OrderHub>("/hubs/order");
        endpoints.MapApiHealthEndpoint();

        return endpoints;
    }
}
