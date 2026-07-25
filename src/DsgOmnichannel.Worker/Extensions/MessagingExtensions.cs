using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace DsgOmnichannel.Worker.Extensions;

/// <summary>
/// Extension methods for configuring MassTransit consumer retry policies.
/// </summary>
internal static class MessagingExtensions
{
    /// <summary>
    /// Applies a global exponential retry policy to every receive endpoint in the bus.
    /// Transient exceptions (TimeoutException, DbUpdateException, HttpRequestException) are retried;
    /// non-transient domain errors (ArgumentException) are skipped immediately.
    ///
    /// Messages that exhaust all retry attempts are automatically moved to the
    /// [queue-name]_error queue by MassTransit's default fault consumer behavior — no extra
    /// configuration is required to enable DLQ behavior.
    /// </summary>
    internal static void UseConsumerRetryPolicy(this IReceiveEndpointConfigurator cfg)
    {
        cfg.UseMessageRetry(r =>
        {
            // Exponential backoff: 3 attempts, starting at 1s, capped at 5s, interval delta 2s
            // Results in delays of approximately: 1s, 3s, 5s
            r.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2));

            // Retry on transient infrastructure faults
            r.Handle<TimeoutException>();
            r.Handle<DbUpdateException>();
            r.Handle<HttpRequestException>();

            // Do not retry on non-transient domain validation errors
            r.Ignore<ArgumentException>();
        });
    }
}
