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

namespace MovieBooking.Infrastructure.BackgroundJobs;

public class SeatCleanupHelper : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SeatCleanupHelper> _logger;

    public SeatCleanupHelper(IServiceProvider serviceProvider, ILogger<SeatCleanupHelper> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
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
                    
                    // We can do this with a batch update if using EF Core 7+ ExecuteUpdateAsync, 
                    // which is much more efficient than fetching all entities. 
                    // Since the user asked for .NET latest (8), we use ExecuteUpdateAsync.
                    
                    var expiredCount = await context.Seats
                        .Where(s => s.Status == SeatStatus.Held && s.HoldExpiryTime < now)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(x => x.Status, SeatStatus.Available)
                            .SetProperty(x => x.UserId, (string?)null)
                            .SetProperty(x => x.HoldExpiryTime, (DateTime?)null),
                            stoppingToken);

                    if (expiredCount > 0)
                    {
                        _logger.LogInformation($"Released {expiredCount} expired seat holds.");
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
