using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DsgOmnichannel.Worker.Services;

/// <summary>
/// Connects the Worker to the API's SignalR hub as a client so it can push
/// order journey events directly to the Angular app, independently of the API.
/// </summary>
public sealed class WorkerSignalRService : IHostedService, IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly ILogger<WorkerSignalRService> _logger;

    public WorkerSignalRService(IConfiguration configuration, ILogger<WorkerSignalRService> logger)
    {
        _logger = logger;

        var hubUrl = configuration["SignalR:HubUrl"]
            ?? throw new InvalidOperationException("SignalR:HubUrl is not configured.");

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.Reconnecting += error =>
        {
            _logger.LogWarning("Worker SignalR connection lost. Reconnecting... {Error}", error?.Message);
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            _logger.LogInformation("Worker SignalR reconnected. ConnectionId={ConnectionId}", connectionId);
            return Task.CompletedTask;
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _connection.StartAsync(cancellationToken);
            _logger.LogInformation("Worker SignalR connected to hub. ConnectionId={ConnectionId}", _connection.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker SignalR failed to connect to hub on startup. Journey events will not be emitted.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _connection.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Pushes an order journey event to the SignalR hub, which relays it to all Angular clients.
    /// </summary>
    public async Task NotifyAsync(
        string displayOrderId,
        string[] components,
        string eventName,
        string[] messages,
        CancellationToken cancellationToken = default)
    {
        if (_connection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Worker SignalR not connected — skipping journey event '{EventName}' for '{OrderId}'.", eventName, displayOrderId);
            return;
        }

        try
        {
            await _connection.InvokeAsync("BroadcastJourneyEvent", new
            {
                displayOrderId,
                components,
                eventName,
                messages,
                timestamp = DateTime.UtcNow.ToString("O")
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker failed to send SignalR journey event '{EventName}'.", eventName);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
