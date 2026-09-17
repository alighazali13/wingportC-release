using System.IO;
using System.Text;
using GamePort.Cashier.Domain.Exceptions;
using GamePort.Cashier.Infrastructure.Backup;
using GamePort.Cashier.Infrastructure.Data;
using GamePort.Cashier.Infrastructure.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

public class HardeningTests
{
    [Fact]
    public async Task Backup_IsNotSupported_ForInMemoryDatabase()
    {
        var options = new DbContextOptionsBuilder<CashierDbContext>()
            .UseInMemoryDatabase($"wingport-backup-{Guid.NewGuid():N}")
            .Options;

        using var context = new CashierDbContext(options);
        var service = new PostgresBackupService(
            context,
            Options.Create(new BackupOptions()),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            NullLogger<PostgresBackupService>.Instance);

        Assert.False(service.IsSupported);

        var result = await service.CreateBackupAsync();

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ExceptionMiddleware_MapsDomainExceptionToBadRequest()
    {
        var middleware = new ApiExceptionMiddleware(
            _ => throw new DomainException("قاعده تجاری نقض شد."),
            NullLogger<ApiExceptionMiddleware>.Instance);

        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        var body = await ReadBodyAsync(context);
        Assert.Contains("قاعده تجاری نقض شد.", body);
    }

    [Fact]
    public async Task ExceptionMiddleware_HidesInternalErrorDetails()
    {
        var middleware = new ApiExceptionMiddleware(
            _ => throw new InvalidOperationException("super-secret-internal-detail"),
            NullLogger<ApiExceptionMiddleware>.Instance);

        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        var body = await ReadBodyAsync(context);
        Assert.DoesNotContain("super-secret-internal-detail", body);
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }
}
