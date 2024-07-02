namespace UserContext.Lib.Extension;

public static class ServiceExtension
{
    public static void AddUserContext(this IServiceCollection serviceCollection)
    {
        // Need to add logging here cause middleware will be executed first
        serviceCollection.AddInternalLogging();
        serviceCollection.AddScoped<IUserContext, UserContext>();
    }
}