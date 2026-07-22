namespace DsgOmnichannel.Api.HealthChecks;

internal sealed record HealthCheckEntryResponse(string Name, string Status, string? Description, TimeSpan Duration);

internal sealed record HealthResponse(string Status, IEnumerable<HealthCheckEntryResponse> Checks);
