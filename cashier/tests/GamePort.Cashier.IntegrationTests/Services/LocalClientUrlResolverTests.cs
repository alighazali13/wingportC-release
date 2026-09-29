using GamePort.Cashier.Infrastructure.Hosting;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

public class LocalClientUrlResolverTests
{
    [Fact]
    public void Resolve_Ipv4Wildcard_PreservesPort()
    {
        var result = LocalClientUrlResolver.Resolve(new[] { "http://0.0.0.0:5210" });

        Assert.Equal("http://127.0.0.1:5210/", result.ToString());
    }

    [Fact]
    public void Resolve_Ipv6Wildcard_PreservesPort()
    {
        var result = LocalClientUrlResolver.Resolve(new[] { "http://[::]:5210" });

        Assert.Equal("http://127.0.0.1:5210/", result.ToString());
    }

    [Fact]
    public void Resolve_ExplicitLoopback_PreservesPort()
    {
        var result = LocalClientUrlResolver.Resolve(new[] { "http://127.0.0.1:5210" });

        Assert.Equal(5210, result.Port);
        Assert.Equal("127.0.0.1", result.Host);
    }

    [Fact]
    public void Resolve_CustomPort_PreservesPort()
    {
        var result = LocalClientUrlResolver.Resolve(new[] { "http://0.0.0.0:6000" });

        Assert.Equal("http://127.0.0.1:6000/", result.ToString());
    }

    [Fact]
    public void Resolve_EmptyCollection_ReturnsFallback()
    {
        var result = LocalClientUrlResolver.Resolve(Array.Empty<string>());

        Assert.Equal("http://127.0.0.1:5210/", result.ToString());
    }

    [Fact]
    public void Resolve_NullCollection_ReturnsFallback()
    {
        var result = LocalClientUrlResolver.Resolve(null!);

        Assert.Equal("http://127.0.0.1:5210/", result.ToString());
    }

    [Fact]
    public void Resolve_MalformedFirst_UsesValidSecond()
    {
        var result = LocalClientUrlResolver.Resolve(new[] { "not-a-url", "http://0.0.0.0:6000" });

        Assert.Equal("http://127.0.0.1:6000/", result.ToString());
    }

    [Fact]
    public void Resolve_MultipleValid_UsesFirst()
    {
        var result = LocalClientUrlResolver.Resolve(new[]
        {
            "http://0.0.0.0:5210",
            "http://0.0.0.0:5211"
        });

        Assert.Equal("http://127.0.0.1:5210/", result.ToString());
    }

    [Fact]
    public void Resolve_AlwaysReturnsLoopback()
    {
        var result = LocalClientUrlResolver.Resolve(new[] { "http://0.0.0.0:5210" });

        Assert.Equal("127.0.0.1", result.Host);
    }

    [Fact]
    public void Resolve_PortIsPreservedFromConfiguration()
    {
        var result = LocalClientUrlResolver.Resolve(new[] { "http://0.0.0.0:9999" });

        Assert.Equal(9999, result.Port);
    }

    [Fact]
    public void Resolve_HttpsScheme_PreservesScheme()
    {
        var result = LocalClientUrlResolver.Resolve(new[] { "https://0.0.0.0:5210" });

        Assert.Equal(5210, result.Port);
        Assert.Equal("127.0.0.1", result.Host);
    }
}
