using GamePort.Cashier.Infrastructure.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

public class CashierServerOptionsTests
{
    [Fact]
    public void Urls_DefaultIsEmpty()
    {
        var options = new CashierServerOptions();

        Assert.Empty(options.Urls);
    }

    [Fact]
    public void GetServerUrls_ReturnsSingleConfiguredUrl()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:Urls:0"] = "http://0.0.0.0:5210"
            })
            .Build();

        var urls = config.GetServerUrls();

        Assert.Single(urls);
        Assert.Equal("http://0.0.0.0:5210", urls[0]);
    }

    [Fact]
    public void GetServerUrls_DoesNotDuplicateDefaultUrl()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:Urls:0"] = "http://0.0.0.0:5210"
            })
            .Build();

        var urls = config.GetServerUrls();

        Assert.Single(urls);
    }

    [Fact]
    public void GetServerUrls_ReturnsDifferentConfiguredUrl()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:Urls:0"] = "http://0.0.0.0:5211"
            })
            .Build();

        var urls = config.GetServerUrls();

        Assert.Single(urls);
        Assert.Equal("http://0.0.0.0:5211", urls[0]);
    }

    [Fact]
    public void GetServerUrls_FallsBackToDefault_WhenConfigMissing()
    {
        var config = new ConfigurationBuilder().Build();

        var urls = config.GetServerUrls();

        Assert.Single(urls);
        Assert.Equal("http://0.0.0.0:5210", urls[0]);
    }

    [Fact]
    public void GetServerUrls_FallsBackToDefault_WhenSectionEmpty()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var urls = config.GetServerUrls();

        Assert.Single(urls);
        Assert.Equal("http://0.0.0.0:5210", urls[0]);
    }

    [Fact]
    public void GetServerUrls_DeduplicatesIdenticalUrls()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:Urls:0"] = "http://0.0.0.0:5210",
                ["Server:Urls:1"] = "http://0.0.0.0:5210"
            })
            .Build();

        var urls = config.GetServerUrls();

        Assert.Single(urls);
        Assert.Equal("http://0.0.0.0:5210", urls[0]);
    }

    [Fact]
    public void GetServerUrls_DeduplicatesCaseInsensitive()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:Urls:0"] = "HTTP://0.0.0.0:5210",
                ["Server:Urls:1"] = "http://0.0.0.0:5210"
            })
            .Build();

        var urls = config.GetServerUrls();

        Assert.Single(urls);
    }

    [Fact]
    public void GetServerUrls_PreservesMultipleDistinctUrls()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:Urls:0"] = "http://127.0.0.1:5210",
                ["Server:Urls:1"] = "http://192.168.1.100:5210"
            })
            .Build();

        var urls = config.GetServerUrls();

        Assert.Equal(2, urls.Length);
        Assert.Contains("http://127.0.0.1:5210", urls);
        Assert.Contains("http://192.168.1.100:5210", urls);
    }

    [Fact]
    public void GetServerUrls_PreservesConfiguredOrder()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:Urls:0"] = "http://192.168.1.100:5210",
                ["Server:Urls:1"] = "http://127.0.0.1:5210"
            })
            .Build();

        var urls = config.GetServerUrls();

        Assert.Equal(2, urls.Length);
        Assert.Equal("http://192.168.1.100:5210", urls[0]);
        Assert.Equal("http://127.0.0.1:5210", urls[1]);
    }

    [Fact]
    public void ConfigurationBinding_DoesNotAppendDefaultToConfigured()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:Urls:0"] = "http://0.0.0.0:5211"
            })
            .Build();

        var options = config.GetSection(CashierServerOptions.SectionName)
            .Get<CashierServerOptions>();

        Assert.NotNull(options);
        Assert.Single(options!.Urls);
        Assert.Equal("http://0.0.0.0:5211", options.Urls[0]);
    }
}
