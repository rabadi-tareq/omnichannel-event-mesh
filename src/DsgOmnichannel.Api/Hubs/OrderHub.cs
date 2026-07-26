using Microsoft.AspNetCore.SignalR;

namespace DsgOmnichannel.Api.Hubs;

public class OrderHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }
}
