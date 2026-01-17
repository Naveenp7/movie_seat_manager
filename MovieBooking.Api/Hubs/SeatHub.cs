using Microsoft.AspNetCore.SignalR;

namespace MovieBooking.Api.Hubs;

public class SeatHub : Hub
{
    // We can add methods here if clients need to invoke server logic via WebSocket,
    // but for now we mainly use it for Server -> Client broadcasting.
}
