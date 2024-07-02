namespace RateLimit.Lib.Middleware;

/// <summary>
/// - Pros:
///     + Memory efficient
///     + Resetting available quota at the end of a unit time window fits certain use cases
/// - Cons:
///     + Spike in traffic at the edges of window could cause more requests than the allowed 
///     quota to go through
/// 
/// *NOTE: This version supporting distributed environment, StringIncrementAsync itself already 
/// handled race condition in distributed environment
/// </summary>
public class FixedWindowRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDatabase _redis;

    #region Redis approach
    // Limit requests per second
    private const int REQUEST_LIMIT = 10;

    // Reset the limit in seconds
    private const int RESET_INTERVAL_SECONDS = 1;
    #endregion

    #region Concurrent Dictionary approach
    private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets = new ConcurrentDictionary<string, RateLimitBucket>();
    private readonly object _lock = new object();
    #endregion

    public FixedWindowRateLimitingMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
    {
        _next = next;
        _redis = redis.GetDatabase();
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
        var cacheKey = $"rate_limit_{key}";

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

    private bool IsRequestAllowed(string key)
    {
        var currentTime = DateTime.UtcNow;
        var bucket = _buckets.GetOrAdd(key, new RateLimitBucket(0, currentTime));

        lock (_lock)
        {
            // Check if the current window has expired (alternative approach to use
            // background job to clean the buckets on intervals)
            if (currentTime >= bucket.WindowStart.Add(TimeSpan.FromSeconds(RESET_INTERVAL_SECONDS)))
            {
                bucket.WindowStart = currentTime;
                bucket.Count = 0;
            }

            // Check if request count is within limits
            if (bucket.Count < REQUEST_LIMIT)
            {
                bucket.Count++;
                return true;
            }

            return false;
        }
    }
}