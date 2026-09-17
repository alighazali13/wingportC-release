using FluentValidation;
using GamePort.Cashier.Contracts.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GamePort.Cashier.Infrastructure.Hosting;

public class ValidationFilter<TRequest> : IEndpointFilter where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            return await next(context);
        }

        var validator = context.HttpContext.RequestServices.GetService<IValidator<TRequest>>();
        if (validator is null)
        {
            return await next(context);
        }

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (!result.IsValid)
        {
            var errors = result.Errors.Select(e => e.ErrorMessage).Distinct().ToList();
            return Results.Json(
                new BaseResponse<object>
                {
                    Success = false,
                    Message = "اعتبارسنجی ورودی ناموفق بود.",
                    Errors = errors
                },
                statusCode: StatusCodes.Status400BadRequest);
        }

        return await next(context);
    }
}

public static class ValidationEndpointExtensions
{
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder) where TRequest : class
        => builder.AddEndpointFilter<ValidationFilter<TRequest>>();
}
