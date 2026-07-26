using Microsoft.AspNetCore.SignalR;

namespace DsgOmnichannel.Api.Hubs;

public class OrderHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called by the Worker SignalR client to broadcast an order journey event
    /// to all Angular clients connected to this hub.
    /// </summary>
    public async Task BroadcastJourneyEvent(object evt)
    {
        await Clients.Others.SendAsync("ReceiveOrderJourneyEvent", evt);
    }
}

