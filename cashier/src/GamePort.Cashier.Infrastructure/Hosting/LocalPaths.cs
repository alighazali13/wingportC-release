using System.Net.Sockets;
using Microsoft.Extensions.Configuration;

namespace GamePort.Cashier.Infrastructure.Hosting;

public static class LocalPaths
{
    public const string DataDirectoryConfigKey = "Storage:DataDirectory";

    public static string DefaultDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GamePort",
        "Cashier");

    public static string Resolve(IConfiguration configuration)
    {
        var configured = configuration[DataDirectoryConfigKey];
        return string.IsNullOrWhiteSpace(configured) ? DefaultDataDirectory : configured;
    }

    public static void EnsureDataDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.Combine(directory, "Logs"));
        Directory.CreateDirectory(Path.Combine(directory, "Data"));
        Directory.CreateDirectory(Path.Combine(directory, "Backups"));
    }
}

public static class PortResolver
{
    public static string[] GetAvailableUrls(IConfiguration configuration)
    {
        var configured = configuration.GetSection("Server:Urls").Get<string[]>() ?? new[] { "http://0.0.0.0:5210" };
        var available = new List<string>();

        foreach (var url in configured)
        {
            if (TryGetPort(url, out var port) && IsPortAvailable(port))
            {
                available.Add(url);
            }
            else if (TryGetPort(url, out var primaryPort))
            {
                var fallback = FindAvailablePort(primaryPort);
                available.Add(BuildUrl(url, fallback));
            }
        }

        if (available.Count == 0)
        {
            var fallbackPort = FindAvailablePort(5210);
            available.Add($"http://0.0.0.0:{fallbackPort}");
        }

        return available.ToArray();
    }

    private static bool TryGetPort(string url, out int port)
    {
        port = 0;
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            var uri = new Uri(url);
            port = uri.Port;
            return port > 0;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var tcp = new TcpClient();
            tcp.Connect("127.0.0.1", port);
            return false;
        }
        catch (SocketException)
        {
            return true;
        }
    }

    private static int FindAvailablePort(int startingPort)
    {
        for (var port = startingPort; port < startingPort + 100; port++)
        {
            if (IsPortAvailable(port))
            {
                return port;
            }
        }

        return startingPort;
    }

    private static string BuildUrl(string original, int newPort)
    {
        try
        {
            var uri = new Uri(original);
            return $"{uri.Scheme}://{uri.Host}:{newPort}";
        }
        catch
        {
            return $"http://0.0.0.0:{newPort}";
        }
    }
}
