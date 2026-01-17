using StackExchange.Redis;
using MovieBooking.Core.Interfaces;
using MovieBooking.Core.Entities;
using MovieBooking.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using MovieBooking.Infrastructure.Hubs;

namespace MovieBooking.Infrastructure.BackgroundJobs;

public class RedisKeyExpiredSubscriber : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RedisKeyExpiredSubscriber> _logger;
    private readonly IHubContext<SeatHub> _hubContext;

    public RedisKeyExpiredSubscriber(
        IConnectionMultiplexer redis,
        IServiceProvider serviceProvider,
        ILogger<RedisKeyExpiredSubscriber> logger,
        IHubContext<SeatHub> hubContext)
    {
        _redis = redis;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _redis.GetSubscriber();
        
        // Subscribe to Key Expiration events on DB 0
        // Requires 'notify-keyspace-events Ex' in Redis Config
        string channel = "__keyevent@0__:expired";
        
        await subscriber.SubscribeAsync(channel, async (channel, value) =>
        {
            try
            {
                string key = value.ToString();
                
                // Expected Key Format: "lock:seat:{showId}:{seatId}"
                if (key.StartsWith("lock:seat:"))
                {
                    _logger.LogInformation($"Detailed Expiration Event received for key: {key}");
                    await ReleaseSeatFromDbAsync(key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Redis expiration event.");
            }
        });

        _logger.LogInformation("Redis Key Expiration Subscriber started.");
        
        // Keep the service alive
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ReleaseSeatFromDbAsync(string lockKey)
    {
        // Parse "lock:seat:{showId}:{seatId}"
        var parts = lockKey.Split(':');
        if (parts.Length < 4) return;
        
        if (!Guid.TryParse(parts[2], out var showId) || !Guid.TryParse(parts[3], out var seatId))
            return;

        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seat = await context.Seats.FirstOrDefaultAsync(s => s.Id == seatId && s.ShowId == showId);

            if (seat != null && seat.Status == SeatStatus.Held)
            {
                // Double check expiry time to be safe (handling race where it was JUST booked)
                if (seat.HoldExpiryTime.HasValue && seat.HoldExpiryTime < DateTime.UtcNow.AddSeconds(5)) // Buffer
                {
                    seat.Status = SeatStatus.Available;
                    seat.UserId = null;
                    seat.HoldExpiryTime = null;
                    
                    await context.SaveChangesAsync();
                    
                    // Real-time broadcast
                    await _hubContext.Clients.All.SendAsync("ReceiveSeatUpdate", seat.Id, (int)SeatStatus.Available, null);
                    await _hubContext.Clients.All.SendAsync("RefreshStats", showId);
                    
                    _logger.LogInformation($"[Reactive] Released seat {seatId} immediately after lock expiry.");
                }
            }
        }
    }
}
