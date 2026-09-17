using System.Security.Claims;
using System.Text.Encodings.Web;
using GamePort.Cashier.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamePort.Cashier.Infrastructure.Authentication;

public class DeviceAuthenticationHandler : AuthenticationHandler<DeviceAuthenticationOptions>
{
    public DeviceAuthenticationHandler(
        IOptionsMonitor<DeviceAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(DeviceAuthenticationDefaults.IdentityHeader, out var identityValues) ||
            !Request.Headers.TryGetValue(DeviceAuthenticationDefaults.SecretHeader, out var secretValues))
        {
            return AuthenticateResult.NoResult();
        }

        var clientIdentity = identityValues.ToString();
        var clientSecret = secretValues.ToString();

        if (string.IsNullOrWhiteSpace(clientIdentity) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return AuthenticateResult.NoResult();
        }

        var authenticator = Context.RequestServices.GetRequiredService<IDeviceAuthenticator>();
        var device = await authenticator.AuthenticateAsync(clientIdentity, clientSecret, Context.RequestAborted);
        if (device is null)
        {
            Logger.LogWarning("Device authentication failed for identity {Identity}", clientIdentity);
            return AuthenticateResult.Fail("Invalid device credentials.");
        }

        var claims = new[]
        {
            new Claim(DeviceAuthenticationDefaults.DeviceIdClaim, device.Id.ToString()),
            new Claim(DeviceAuthenticationDefaults.DeviceNameClaim, device.Name),
            new Claim(DeviceAuthenticationDefaults.DeviceTypeClaim, device.Type.ToString())
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
