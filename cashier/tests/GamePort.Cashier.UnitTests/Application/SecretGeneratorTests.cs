using GamePort.Cashier.Application.Common;
using Xunit;

namespace GamePort.Cashier.UnitTests.Application;

public class SecretGeneratorTests
{
    [Fact]
    public void Generate_ReturnsNonEmptyUniqueValues()
    {
        var first = SecretGenerator.Generate();
        var second = SecretGenerator.Generate();

        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GenerateIdentity_UsesPrefixAndIsUnique()
    {
        var first = SecretGenerator.GenerateIdentity("dev");
        var second = SecretGenerator.GenerateIdentity("dev");

        Assert.StartsWith("dev-", first);
        Assert.NotEqual(first, second);
    }
}

public class ResultTests
{
    [Fact]
    public void Success_ExposesValueAndNoError()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_ExposesErrorAndNoValue()
    {
        var result = Result<int>.Failure("boom");

        Assert.False(result.IsSuccess);
        Assert.Equal("boom", result.Error);
        Assert.Equal(0, result.Value);
    }
}
