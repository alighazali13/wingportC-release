namespace GamePort.Cashier.Services;

public interface ISessionContext
{
    string? Token { get; }
    Guid? EmployeeId { get; }
    string? Name { get; }
    string? Username { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }

    void SignIn(string token, Guid employeeId, string name, string username, string role);
    void SignOut();
}

public class SessionContext : ISessionContext
{
    public string? Token { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public string? Name { get; private set; }
    public string? Username { get; private set; }
    public string? Role { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Token);

    public void SignIn(string token, Guid employeeId, string name, string username, string role)
    {
        Token = token;
        EmployeeId = employeeId;
        Name = name;
        Username = username;
        Role = role;
    }

    public void SignOut()
    {
        Token = null;
        EmployeeId = null;
        Name = null;
        Username = null;
        Role = null;
    }
}
