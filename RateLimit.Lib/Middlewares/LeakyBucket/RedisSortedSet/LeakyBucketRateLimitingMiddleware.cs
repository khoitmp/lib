namespace RateLimit.Lib.Middleware.LeakyBucket.RedisSortedSet;

/// <summary>
/// The leaking bucket algorithm is similar to the token bucket except that requests
/// are processed at a fixed rate. It is usually implemented with a first-in-first-out
/// (FIFO) queue. When a request arrives, the system checks if the queue if full. If it's
/// not full, the request is added to the queue. Otherwise, the request is dropped. Requests
/// are pulled from the queue and processed at regular intervals
/// 
/// * Pros:
///     + Memory efficient given the limited queue size
///     + Requests are processed at a fixed rate therefore it is suitable for use 
///     cases that a stable outflow rate is needed
/// * Cons:
///     + A burst of traffic fills up the queue with old requests, and if they are not
///     processed in time, recent requests will be rate limited
/// 
/// *NOTE: This version supporting in distributed environment as SortedSet operations 
/// are atomic, ensuring that no other operations can interfere with them
public class LeakyBucketRateLimitingMiddleware : BaseRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _redisDb;

    // Max requests in the bucket
    private static int REQUEST_LIMIT;

    // Leak rate in requests per second (n requests per second)
    private static int LEAK_RATE;

    public LeakyBucketRateLimitingMiddleware(RequestDelegate next, IConfiguration configuration, IConnectionMultiplexer redis)
        : base(next)
    {
        _next = next;

        _redis = redis;
        _redisDb = _redis.GetDatabase();

        REQUEST_LIMIT = Convert.ToInt32(configuration["RateLimit:Limit"] ?? "10");
        LEAK_RATE = Convert.ToInt32(configuration["RateLimit:LeakRate"] ?? "1");
    }

    protected override async Task<bool> IsRequestAllowedAsync(string key)
    {
        var cacheKey = CacheKeys.LEAKY_BUCKET.GetString(key);
        var leakInterval = TimeSpan.FromSeconds(LEAK_RATE);

        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Remove expired tokens (similar with queue interval removal operation)
        await _redisDb.SortedSetRemoveRangeByScoreAsync(cacheKey, double.NegativeInfinity, currentTime - leakInterval.TotalMilliseconds * REQUEST_LIMIT);

        // Add current token
        await _redisDb.SortedSetAddAsync(cacheKey, currentTime, currentTime);

        // Check current bucket size
        var bucketSize = await _redisDb.SortedSetLengthAsync(cacheKey);

        return bucketSize <= REQUEST_LIMIT;
    }
}