namespace UserContext.Lib.Interface;

public interface IUserContext
{
    Guid Id { get; }
    string UserName { get; }
    string Role { get; }
    IUserContext SetId(Guid id);
    IUserContext SetUserName(string userName);
    IUserContext SetRole(string role);
    bool IsAdmin();
    bool IsUser();
}