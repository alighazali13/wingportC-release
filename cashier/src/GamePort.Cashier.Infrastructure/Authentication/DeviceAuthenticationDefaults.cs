namespace GamePort.Cashier.Infrastructure.Authentication;

public static class DeviceAuthenticationDefaults
{
    public const string Scheme = "DeviceScheme";
    public const string IdentityHeader = "X-Device-Identity";
    public const string SecretHeader = "X-Device-Secret";
    public const string DeviceIdClaim = "device_id";
    public const string DeviceNameClaim = "device_name";
    public const string DeviceTypeClaim = "device_type";
}
