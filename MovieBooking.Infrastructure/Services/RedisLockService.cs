using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace MovieBooking.Infrastructure.Services;

public class RedisLockService : IDistributedLockService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisLockService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<bool> AcquireLockAsync(string key, TimeSpan expiry)
    {
        var db = _redis.GetDatabase();
        // SET resource_name my_random_value NX PX 30000
        // NX = Only set if not exists
        return await db.StringSetAsync(key, "locked", expiry, When.NotExists);
    }

    public async Task ReleaseLockAsync(string key)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(key);
    }
}
