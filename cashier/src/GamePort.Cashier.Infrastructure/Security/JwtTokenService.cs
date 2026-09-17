using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Application.Security;
using GamePort.Cashier.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GamePort.Cashier.Infrastructure.Security;

public class JwtTokenService : ITokenService
{
    public const string PermissionClaim = "perm";
    public const string EmployeeIdClaim = "employee_id";
    public const string RoleClaim = "role";

    private readonly JwtOptions _options;
    private readonly SigningCredentials _credentials;

    public JwtTokenService(IOptions<JwtOptions> options, SigningCredentials credentials)
    {
        _options = options.Value;
        _credentials = credentials;
    }

    public AuthToken CreateToken(Employee employee)
    {
        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddHours(Math.Max(1, _options.TokenLifetimeHours));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, employee.Id.ToString()),
            new(EmployeeIdClaim, employee.Id.ToString()),
            new(ClaimTypes.Name, employee.Name),
            new(JwtRegisteredClaimNames.UniqueName, employee.Username),
            new(RoleClaim, employee.Role)
        };

        foreach (var permission in Roles.GetPermissions(employee.Role))
        {
            claims.Add(new Claim(PermissionClaim, permission));
        }

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: _credentials);

        var value = new JwtSecurityTokenHandler().WriteToken(token);
        return new AuthToken(value, expiresAt, employee.Id, employee.Name, employee.Username, employee.Role);
    }
}
