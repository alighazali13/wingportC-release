using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using GamePort.Cashier.Application.Security;
using GamePort.Cashier.Infrastructure.Hosting;
using GamePort.Cashier.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamePort.Cashier.Infrastructure.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IOptions<CashierServerOptions> _serverOptions;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<CashierServerOptions> serverOptions)
        : base(options, logger, encoder)
    {
        _serverOptions = serverOptions;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredKey = _serverOptions.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.HeaderName, out var provided) ||
            !FixedTimeEquals(provided.ToString(), configuredKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "ApiClient"),
            new(JwtTokenService.EmployeeIdClaim, Guid.Empty.ToString()),
            new(JwtTokenService.RoleClaim, Roles.Admin)
        };

        claims.AddRange(Permissions.All.Select(p => new Claim(JwtTokenService.PermissionClaim, p)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
