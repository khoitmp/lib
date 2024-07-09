namespace RateLimit.Lib.Middleware.FixedWindow.ConcurrentDictionary;

/// <summary>
/// *NOTE: This version not supporting in distributed environment
/// </summary>
public class FixedWindowRateLimitingMiddleware : BaseRateLimitingMiddleware
{
    private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets = new ConcurrentDictionary<string, RateLimitBucket>();
    private readonly object _lock = new object();

    private static int REQUEST_LIMIT;
    private static int RESET_INTERVAL_SECONDS;

    public FixedWindowRateLimitingMiddleware(RequestDelegate next, IConfiguration configuration, IConnectionMultiplexer redis)
        : base(next)
    {
        REQUEST_LIMIT = Convert.ToInt32(configuration["RateLimit:Limit"] ?? "10");
        RESET_INTERVAL_SECONDS = Convert.ToInt32(configuration["RateLimit:ResetIntervalSeconds"] ?? "1");
    }

    protected override Task<bool> IsRequestAllowedAsync(string key)
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
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }
}