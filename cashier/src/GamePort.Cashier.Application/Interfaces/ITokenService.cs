using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Application.Interfaces;

public record AuthToken(
    string Token,
    DateTime ExpiresAt,
    Guid EmployeeId,
    string Name,
    string Username,
    string Role);

public interface ITokenService
{
    AuthToken CreateToken(Employee employee);
}
