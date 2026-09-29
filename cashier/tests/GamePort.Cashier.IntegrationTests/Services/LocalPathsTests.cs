using GamePort.Cashier.Infrastructure.Hosting;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

public class LocalPathsTests : IDisposable
{
    private readonly string _testDir;

    public LocalPathsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"GamePortTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    [Fact]
    public void InitializeConfiguration_DoesNotOverwrite_WhenTargetExists()
    {
        var dataDir = Path.Combine(_testDir, "data");
        Directory.CreateDirectory(dataDir);

        var targetConfig = Path.Combine(dataDir, "appsettings.json");
        var customContent = """{"Server":{"Urls":["http://0.0.0.0:9999"]}}""";
        File.WriteAllText(targetConfig, customContent);

        LocalPaths.InitializeConfiguration(dataDir);

        var result = File.ReadAllText(targetConfig);
        Assert.Equal(customContent, result);
    }

    [Fact]
    public void InitializeConfiguration_DoesNotThrow_WhenSourceMissing()
    {
        var dataDir = Path.Combine(_testDir, "data");
        Directory.CreateDirectory(dataDir);

        var ex = Record.Exception(() => LocalPaths.InitializeConfiguration(dataDir));
        Assert.Null(ex);

        Assert.False(File.Exists(Path.Combine(dataDir, "appsettings.json")));
    }

    [Fact]
    public void EnsureDataDirectory_CreatesAllSubdirectories()
    {
        var dataDir = Path.Combine(_testDir, "newdata");

        LocalPaths.EnsureDataDirectory(dataDir);

        Assert.True(Directory.Exists(dataDir));
        Assert.True(Directory.Exists(Path.Combine(dataDir, "Logs")));
        Assert.True(Directory.Exists(Path.Combine(dataDir, "Data")));
        Assert.True(Directory.Exists(Path.Combine(dataDir, "Backups")));
    }

    [Fact]
    public void DefaultDataDirectory_ContainsGamePortCashier()
    {
        var path = LocalPaths.DefaultDataDirectory;

        Assert.Contains("GamePort", path);
        Assert.Contains("Cashier", path);
    }

    [Fact]
    public void InitializeConfiguration_CopiesSourceToTarget()
    {
        var sourceDir = Path.Combine(_testDir, "source");
        var dataDir = Path.Combine(_testDir, "data");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(dataDir);

        var sourceConfig = Path.Combine(sourceDir, "appsettings.json");
        var targetConfig = Path.Combine(dataDir, "appsettings.json");
        var configContent = """{"Server":{"Urls":["http://0.0.0.0:5211"]}}""";
        File.WriteAllText(sourceConfig, configContent);

        var originalBase = AppContext.BaseDirectory;
        try
        {
            typeof(AppContext).GetMethod("SetData")?.Invoke(null, new object[] { "APP_CONTEXT_BASE_DIRECTORY", sourceDir });
        }
        catch
        {
            return;
        }

        try
        {
            LocalPaths.InitializeConfiguration(dataDir);

            Assert.True(File.Exists(targetConfig));
            var content = File.ReadAllText(targetConfig);
            Assert.Contains("5211", content);
        }
        finally
        {
            try
            {
                typeof(AppContext).GetMethod("SetData")?.Invoke(null, new object[] { "APP_CONTEXT_BASE_DIRECTORY", originalBase });
            }
            catch
            {
                // best effort restore
            }
        }
    }
}
