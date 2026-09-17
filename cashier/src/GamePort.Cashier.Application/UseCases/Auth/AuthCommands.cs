namespace GamePort.Cashier.Application.UseCases.Auth;

public record LoginCommand(
    string Username,
    string Password);

public record LoginResult(
    string Token,
    DateTime ExpiresAt,
    Guid EmployeeId,
    string Name,
    string Username,
    string Role);

public record ChangePasswordCommand(
    Guid EmployeeId,
    string CurrentPassword,
    string NewPassword);
