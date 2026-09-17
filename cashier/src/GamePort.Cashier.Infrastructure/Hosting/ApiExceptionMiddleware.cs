using System.Text.Encodings.Web;
using System.Text.Json;
using GamePort.Cashier.Contracts.Common;
using GamePort.Cashier.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GamePort.Cashier.Infrastructure.Hosting;

public class ApiExceptionMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain rule violation on {Method} {Path}.", context.Request.Method, context.Request.Path);
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected; nothing to write.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error on {Method} {Path}.", context.Request.Method, context.Request.Path);
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, "خطای داخلی سرور رخ داد.");
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new BaseResponse<object>
        {
            Success = false,
            Message = message,
            Errors = new List<string> { message }
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions));
    }
}
