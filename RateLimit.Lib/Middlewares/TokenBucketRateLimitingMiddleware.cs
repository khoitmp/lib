namespace RateLimit.Lib.Middleware;

/// <summary>
/// - Pros:
///     + Memory efficient
///     + Token bucket allows a burst of traffic for short periods. A request can go through
///     as long as there are tokens left
/// - Cons:
///     + Two parameters in the algorithm are bucket size and token refill rate. However, it might
/// be challenging to tune them properly
///  
/// *NOTE: This version not supporting in distributed environment, StringGetAsync & StringSetAsync cannot handle race condition (alternative approach 
/// could be using Lua script or Redis Sorted Set)
/// </summary>
public class TokenBucketRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDatabase _redisDb;

    // Max tokens in the bucket
    private const int MAX_TOKENS = 10;

    // Interval in seconds to refill tokens
    private const int REFILL_INTERVAL_SECONDS = 1;

    // Number of tokens to add each interval
    private const int REFILL_TOKENS = 10;

    public TokenBucketRateLimitingMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
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
        var cacheKey = $"token_bucket_{key}";
        var tokenKey = $"{cacheKey}_tokens";
        var timestampKey = $"{cacheKey}_timestamp";

        var transaction = _redisDb.CreateTransaction();

        // Get the current token count and the last refill timestamp
        var tokenCountTask = transaction.StringGetAsync(tokenKey);
        var lastRefillTimestampTask = transaction.StringGetAsync(timestampKey);

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

        // Always round down
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
        await updateTransaction.StringSetAsync(tokenKey, tokenCount);
        await updateTransaction.StringSetAsync(timestampKey, currentTime);

        // Execute the update transaction
        var updateResult = await updateTransaction.ExecuteAsync();

        return updateResult;
    }
}