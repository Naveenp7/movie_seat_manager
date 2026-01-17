using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace MovieBooking.Infrastructure.Services;

public interface IDistributedLockService
{
    Task<bool> AcquireLockAsync(string key, TimeSpan expiry);
    Task ReleaseLockAsync(string key);
}

// SIMULATION: This acts like a Redis instance.
// In a real microservices architecture, this would use StackExchange.Redis.
public class MockRedisLockService : IDistributedLockService
{
    // Key -> ExpiryTime
    private static readonly ConcurrentDictionary<string, DateTime> _locks = new();

    public Task<bool> AcquireLockAsync(string key, TimeSpan expiry)
    {
        var now = DateTime.UtcNow;
        
        // 1. Clean up if existing lock expired (Lazy expiration)
        if (_locks.TryGetValue(key, out var expiration))
        {
            if (expiration < now)
            {
                _locks.TryRemove(key, out _);
            }
            else
            {
                // Lock is actively held
                return Task.FromResult(false); 
            }
        }

        // 2. Try to add new lock
        var newExpiry = now.Add(expiry);
        bool success = _locks.TryAdd(key, newExpiry);
        
        // Double check race condition if added successfully? 
        // ConcurrentDictionary handles atomic adds.
        return Task.FromResult(success);
    }

    public Task ReleaseLockAsync(string key)
    {
        _locks.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
