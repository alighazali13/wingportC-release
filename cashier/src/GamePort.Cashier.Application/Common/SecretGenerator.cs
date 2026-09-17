using System.Security.Cryptography;

namespace GamePort.Cashier.Application.Common;

public static class SecretGenerator
{
    public static string Generate(int byteLength = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes);
    }

    public static string GenerateIdentity(string prefix = "dev")
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }
}
