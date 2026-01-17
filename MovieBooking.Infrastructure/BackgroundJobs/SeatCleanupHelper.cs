using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MovieBooking.Core.Entities;
using MovieBooking.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using MovieBooking.Infrastructure.Hubs;

namespace MovieBooking.Infrastructure.BackgroundJobs;

public class SeatCleanupHelper : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SeatCleanupHelper> _logger;
    private readonly IHubContext<SeatHub> _hubContext;

    public SeatCleanupHelper(IServiceProvider serviceProvider, ILogger<SeatCleanupHelper> logger, IHubContext<SeatHub> hubContext)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    
                    // Release expired holds
                    // Logic: Status is Held AND Expiry < Now
                    
                    var now = DateTime.UtcNow;
                    
                    // Efficient Bulk Update is great for DB, but we need to notify clients.
                    // ExecuteUpdateAsync doesn't return the IDs modified easily in one go combined with the update logic unless we fetch first.
                    // For SignalR, we unfortunately need to know WHICH seats expired.
                    // Performance Trade-off: Fetch expired seats, then update them.

                    var expiredSeats = await context.Seats
                        .Where(s => s.Status == SeatStatus.Held && s.HoldExpiryTime < DateTime.UtcNow)
                        .ToListAsync(stoppingToken);

                    if (expiredSeats.Any())
                    {
                        foreach (var seat in expiredSeats)
                        {
                            seat.Status = SeatStatus.Available;
                            seat.UserId = null;
                            seat.HoldExpiryTime = null;
                            // SignalR Broadcast
                            await _hubContext.Clients.All.SendAsync("ReceiveSeatUpdate", seat.Id, (int)SeatStatus.Available, null);
                        }
                        
                        await context.SaveChangesAsync(stoppingToken);
                        await _hubContext.Clients.All.SendAsync("RefreshStats", expiredSeats.First().ShowId);
                        
                        _logger.LogInformation($"Released {expiredSeats.Count} expired seats.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing expired seats.");
            }

            // Wait for 5 seconds before next check
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
