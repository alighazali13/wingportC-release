namespace GamePort.Cashier.Application.UseCases.Employees;

public record CreateEmployeeCommand(
    string Name,
    string Username,
    string Password,
    string Role,
    string? OperatorInfo);

public record EmployeeResult(
    Guid EmployeeId,
    string Name,
    string Username,
    string Role,
    bool IsActive);

public record SetEmployeeActiveCommand(
    Guid EmployeeId,
    bool IsActive,
    string? OperatorInfo);
