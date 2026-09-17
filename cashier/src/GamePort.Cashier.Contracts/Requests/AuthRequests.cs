namespace GamePort.Cashier.Contracts.Requests;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class CreateEmployeeRequest
{
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Operator";
}

public class SetEmployeeActiveRequest
{
    public Guid EmployeeId { get; set; }
    public bool IsActive { get; set; }
}

public class ClockInRequest
{
    public Guid? EmployeeId { get; set; }
    public string? Notes { get; set; }
}

public class ClockOutRequest
{
    public Guid? EmployeeId { get; set; }
}
