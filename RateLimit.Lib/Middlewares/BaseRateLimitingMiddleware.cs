namespace RateLimit.Lib.Middleware;

public abstract class BaseRateLimitingMiddleware
{
    private readonly RequestDelegate _next;

    public BaseRateLimitingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string userId = context.Request.Headers[HeaderKeys.X_USER_ID];
        if (string.IsNullOrEmpty(userId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync(Messages.MISSING.GetString(HeaderKeys.X_USER_ID));
            return;
        }

        if (!await IsRequestAllowedAsync(userId))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsync(Messages.RATE_LIMIT_EXCEEDED);
            return;
        }

        await _next(context);
    }

    protected abstract Task<bool> IsRequestAllowedAsync(string key);
}