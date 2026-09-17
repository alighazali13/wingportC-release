using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Application.Interfaces;

public interface IDeviceAuthenticator
{
    Task<Device?> AuthenticateAsync(string clientIdentity, string clientSecret, CancellationToken cancellationToken = default);
}
