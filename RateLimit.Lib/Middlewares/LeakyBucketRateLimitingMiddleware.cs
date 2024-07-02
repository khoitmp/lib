namespace RateLimit.Lib.Middleware;

/// <summary>
/// Pros:
/// - Memory efficient given the limited queue size
/// - Requests are processed at a fixed rate therefore it is suitable for use 
/// cases that a stable outflow rate is needed
/// Cons:
/// - A burst of traffic fills up the queue with old requests, and if they are not
/// processed in time, recent requests will be rate limited
/// 
/// *NOTE: This version not supporting in distributed environment, HashSetAsync cannot handle race condition (alternative approach 
/// could be using Lua script or Redis Sorted Set)
/// </summary>
public class LeakyBucketRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDatabase _redisDb;

    // Max requests in the bucket
    private const int MAX_REQUESTS = 10;

    // Leak rate in requests per second
    private const int LEAK_RATE = 1;

    public LeakyBucketRateLimitingMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
    {
        _next = next;
        _redisDb = redis.GetDatabase();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string userId = context.Request.Headers[HeaderKeys.X_USER_ID];
        if (string.IsNullOrEmpty(userId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Missing x-user-id");
            return;
        }

        if (!await IsRequestAllowedAsync(userId))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsync("Rate limit exceeded");
            return;
        }

        await _next(context);
    }

    private async Task<bool> IsRequestAllowedAsync(string key)
    {
        var requestCountCacheKey = "request_count";
        var lastTimeCacheKey = "last_time";
        var cacheKey = $"leaky_bucket_{key}";

        // Get the current timestamp in seconds
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Get or create the leaky bucket entry in Redis
        var bucketExists = await _redisDb.KeyExistsAsync(cacheKey);
        if (!bucketExists)
        {
            // Initialize the bucket with initial values
            await _redisDb.HashSetAsync(cacheKey, new HashEntry[] {
                new HashEntry(requestCountCacheKey, "0"),
                new HashEntry(lastTimeCacheKey, currentTime.ToString())
            });
        }

        // Get current state of the bucket
        var hashEntries = await _redisDb.HashGetAllAsync(cacheKey);
        var requestCount = int.Parse(hashEntries.FirstOrDefault(e => e.Name == requestCountCacheKey).Value);
        var lastTime = long.Parse(hashEntries.FirstOrDefault(e => e.Name == lastTimeCacheKey).Value);

        // Calculate the elapsed time since the last request
        var elapsedTime = currentTime - lastTime;

        // Leak requests over time
        var leakedRequests = (int)(elapsedTime * LEAK_RATE);
        requestCount = Math.Max(0, requestCount - leakedRequests);

        if (requestCount < MAX_REQUESTS)
        {
            // Update the bucket with the new request count and time
            await _redisDb.HashSetAsync(cacheKey, new HashEntry[] {
                new HashEntry(requestCountCacheKey, (requestCount + 1).ToString()),
                new HashEntry(lastTimeCacheKey, currentTime.ToString())
            });

            return true;
        }

        return false;
    }
}