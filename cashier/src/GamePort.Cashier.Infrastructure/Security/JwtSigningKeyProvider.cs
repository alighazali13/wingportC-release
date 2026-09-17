using System.Security.Cryptography;

namespace GamePort.Cashier.Infrastructure.Security;

public static class JwtSigningKeyProvider
{
    private const int KeySizeBytes = 64;

    public static byte[] GetOrCreateKey(string baseDirectory)
    {
        var directory = Path.Combine(baseDirectory, "Data");
        Directory.CreateDirectory(directory);
        var keyPath = Path.Combine(directory, "jwt-signing.key");

        if (File.Exists(keyPath))
        {
            var existing = File.ReadAllText(keyPath).Trim();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                try
                {
                    return Convert.FromBase64String(existing);
                }
                catch (FormatException)
                {
                    // Fall through and regenerate a valid key.
                }
            }
        }

        var key = RandomNumberGenerator.GetBytes(KeySizeBytes);
        File.WriteAllText(keyPath, Convert.ToBase64String(key));
        return key;
    }
}
