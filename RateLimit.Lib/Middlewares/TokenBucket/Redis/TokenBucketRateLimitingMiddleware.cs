namespace RateLimit.Lib.Middleware.TokenBucket.Redis;

/// <summary>
/// A token bucket is a container that has pre-defiend capacity. Tokens are put in
/// the bucket at preset rates periodically. Once the bucket is full, no more tokens
/// are added
///
/// * Pros:
///     + Memory efficient
///     + Token bucket allows a burst of traffic for short periods. A request can go 
///     through as long as there are tokens left
/// * Cons:
///     + Two parameters in the algorithm are bucket size and token refill rate. However, 
///     it might be challenging to tune them properly
///
/// *NOTE: This version not supporting in distributed environment, StringGetAsync & 
/// StringSetAsync cannot handle race condition (alternative approach could be using 
/// Lua script or Redis Sorted Set)
/// </summary>
public class TokenBucketRateLimitingMiddleware : BaseRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDatabase _redisDb;

    // Max tokens in the bucket
    private static int MAX_TOKENS;

    // Interval in seconds to refill tokens
    private static int REFILL_INTERVAL_SECONDS;

    // Number of tokens to add each interval
    private static int REFILL_TOKENS;

    public TokenBucketRateLimitingMiddleware(RequestDelegate next, IConfiguration configuration, IConnectionMultiplexer redis)
        : base(next)
    {
        _next = next;
        _redisDb = redis.GetDatabase();

        MAX_TOKENS = Convert.ToInt32(configuration["RateLimit:Limit"] ?? "10");
        REFILL_INTERVAL_SECONDS = Convert.ToInt32(configuration["RateLimit:ResetIntervalSeconds"] ?? "1");
        REFILL_TOKENS = Convert.ToInt32(configuration["RateLimit:Refill"] ?? "10");
    }

    protected override async Task<bool> IsRequestAllowedAsync(string key)
    {
        var tokenCacheKey = CacheKeys.TOKEN_BUCKET_TOKENS.GetString(key);
        var tsCacheKey = CacheKeys.TOKEN_BUCKET_TIMESTAMP.GetString(key);

        var transaction = _redisDb.CreateTransaction();

        // Get the current token count and the last refill timestamp
        var tokenCountTask = transaction.StringGetAsync(tokenCacheKey);
        var lastRefillTimestampTask = transaction.StringGetAsync(tsCacheKey);

        // Execute the transaction
        if (!await transaction.ExecuteAsync())
        {
            // If transaction fails, deny the request
            return false;
        }

        // Retrieve token count and last refill timestamp from tasks
        RedisValue tokenValue = await tokenCountTask;
        RedisValue timestampValue = await lastRefillTimestampTask;

        // Parse Redis values to integers (or use default values if they are null or not parseable)
        int tokenCount = tokenValue.HasValue ? (int)tokenValue : MAX_TOKENS;
        long lastRefillTimestamp = timestampValue.HasValue ? (long)timestampValue : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Calculate the number of tokens to add based on the elapsed time
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long elapsedTime = currentTime - lastRefillTimestamp;

        // Always round down (the result can either be 0 or REFILL_TOKENS)
        int tokensToAdd = (int)(elapsedTime / REFILL_INTERVAL_SECONDS) * REFILL_TOKENS;

        // Update the token count
        tokenCount = Math.Min(tokenCount + tokensToAdd, MAX_TOKENS);

        // Check if there are enough tokens
        if (tokenCount < 1)
        {
            return false;
        }

        // Deduct a token for the current request
        tokenCount--;

        // Update Redis with the new token count and timestamp
        var updateTransaction = _redisDb.CreateTransaction();
        await updateTransaction.StringSetAsync(tokenCacheKey, tokenCount);
        await updateTransaction.StringSetAsync(tsCacheKey, currentTime);

        // Execute the update transaction
        var updateResult = await updateTransaction.ExecuteAsync();

        return updateResult;
    }
}