using DsgOmnichannel.Api.HealthChecks;

namespace DsgOmnichannel.Api.Endpoints;

internal static class ApiEndpointRouteBuilderExtensions
{
    internal static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapControllers();
        endpoints.MapGet("/", () => Results.Redirect("/swagger"));
        endpoints.MapApiHealthEndpoint();
        endpoints.MapTestPublishEndpoint();

        return endpoints;
    }
}
