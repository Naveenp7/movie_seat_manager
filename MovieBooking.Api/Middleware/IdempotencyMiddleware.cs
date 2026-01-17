using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace MovieBooking.Api.Middleware;

public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDistributedCache _cache;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, IDistributedCache cache, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method != "POST" || !context.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey))
        {
            await _next(context);
            return;
        }

        string key = $"idemp:{idempotencyKey}";
        var cachedResponse = await _cache.GetStringAsync(key);

        if (!string.IsNullOrEmpty(cachedResponse))
        {
            _logger.LogInformation($"Idempotency hit for key: {idempotencyKey}");
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(cachedResponse);
            return;
        }

        // Capture response
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await _next(context);

        if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseText = await new StreamReader(context.Response.Body).ReadToEndAsync();
            context.Response.Body.Seek(0, SeekOrigin.Begin);

            // Cache for 10 minutes
            await _cache.SetStringAsync(key, responseText, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });
        }

        await responseBody.CopyToAsync(originalBodyStream);
    }
}
