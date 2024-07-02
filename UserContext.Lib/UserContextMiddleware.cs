namespace UserContext.Lib;

public class UserContextMiddleware
{
    private readonly RequestDelegate _next;

    public UserContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var token = context.Request.Headers.GetHeaderValue(HeaderKeys.AUTHORIZATION);
        var validation = token.ValidateToken();

        if (!validation.Valid)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorize");
        }
        else
        {
            var userContext = context.RequestServices.GetService(typeof(IUserContext)) as IUserContext;

            userContext.SetId(validation.Claims.Id);
            userContext.SetUserName(validation.Claims.UserName);
            userContext.SetRole(validation.Claims.Role);
        }

        await _next(context);
    }
}