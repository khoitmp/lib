namespace UserContext.Lib;

public class UserContext : IUserContext
{
    public Guid Id { get; private set; }
    public string UserName { get; private set; }
    public string Role { get; private set; }

    public IUserContext SetId(Guid id)
    {
        Id = id;
        return this;
    }

    public IUserContext SetUserName(string userName)
    {
        UserName = userName;
        return this;
    }

    public IUserContext SetRole(string role)
    {
        Role = role;
        return this;
    }

    public bool IsAdmin()
    {
        return Role == Roles.ADMIN;
    }

    public bool IsUser()
    {
        return Role == Roles.USER;
    }
}