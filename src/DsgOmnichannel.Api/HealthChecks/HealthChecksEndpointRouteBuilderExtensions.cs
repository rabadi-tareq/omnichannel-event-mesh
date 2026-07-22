using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DsgOmnichannel.Api.HealthChecks;

internal static class HealthChecksEndpointRouteBuilderExtensions
{
    internal static IEndpointRouteBuilder MapApiHealthEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/health", async (HealthCheckService healthCheckService, CancellationToken cancellationToken) =>
        {
            var report = await healthCheckService.CheckHealthAsync(cancellationToken);

            var response = new HealthResponse(
                report.Status.ToString(),
                report.Entries.Select(entry => new HealthCheckEntryResponse(
                    entry.Key,
                    entry.Value.Status.ToString(),
                    entry.Value.Description,
                    entry.Value.Duration)));

            return Results.Json(
                response,
                statusCode: report.Status == HealthStatus.Healthy
                    ? StatusCodes.Status200OK
                    : StatusCodes.Status503ServiceUnavailable);
        })
        .WithName("Health")
        .Produces<HealthResponse>(StatusCodes.Status200OK)
        .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }
}
