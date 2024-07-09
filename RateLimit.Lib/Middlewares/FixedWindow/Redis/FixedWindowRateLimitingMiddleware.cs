namespace RateLimit.Lib.Middleware.FixedWindow.Redis;

/// <summary>
/// The algorithm devides the timeline into fix-sized time windows and assign a 
/// counter for each window, each request increments the counter by one. Once the 
/// counter reaches the pre-defiend threshold, new requests are dropped until a 
/// new time window starts
/// 
/// * Pros:
///     + Memory efficient
///     + Resetting available quota at the end of a unit time window fits certain 
///     use cases
/// * Cons:
///     + Spike in traffic at the edges of window could cause more requests than 
///     the allowed quota to go through
/// 
/// *NOTE: This version supporting distributed environment, StringIncrementAsync itself already 
/// handled race condition in distributed environment
/// </summary>
public class FixedWindowRateLimitingMiddleware : BaseRateLimitingMiddleware
{
    private readonly IDatabase _redis;

    private static int REQUEST_LIMIT;
    private static int RESET_INTERVAL_SECONDS;

    public FixedWindowRateLimitingMiddleware(RequestDelegate next, IConfiguration configuration, IConnectionMultiplexer redis)
        : base(next)
    {
        _redis = redis.GetDatabase();

        REQUEST_LIMIT = Convert.ToInt32(configuration["RateLimit:Limit"] ?? "10");
        RESET_INTERVAL_SECONDS = Convert.ToInt32(configuration["RateLimit:ResetIntervalSeconds"] ?? "1");
    }

    protected override async Task<bool> IsRequestAllowedAsync(string key)
    {
        var cacheKey = CacheKeys.FIXED_RATE.GetString(key);

        // Atomically increment the request count
        var currentCount = await _redis.StringIncrementAsync(cacheKey);

        // If this is the first request, set the expiration to 1 second
        if (currentCount == 1)
        {
            await _redis.KeyExpireAsync(cacheKey, TimeSpan.FromSeconds(RESET_INTERVAL_SECONDS));
        }

        // Allow the request if the current count is within the limit
        return currentCount <= REQUEST_LIMIT;
    }
}