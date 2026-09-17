using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Infrastructure.Authentication;

public class DeviceAuthenticator : IDeviceAuthenticator
{
    private readonly IDeviceRepository _devices;
    private readonly ISecretHasher _secretHasher;

    public DeviceAuthenticator(IDeviceRepository devices, ISecretHasher secretHasher)
    {
        _devices = devices;
        _secretHasher = secretHasher;
    }

    public async Task<Device?> AuthenticateAsync(string clientIdentity, string clientSecret, CancellationToken cancellationToken = default)
    {
        var device = await _devices.GetByClientIdentityAsync(clientIdentity);
        if (device is null || string.IsNullOrEmpty(device.ClientSecretHash))
        {
            return null;
        }

        return _secretHasher.Verify(clientSecret, device.ClientSecretHash) ? device : null;
    }
}
