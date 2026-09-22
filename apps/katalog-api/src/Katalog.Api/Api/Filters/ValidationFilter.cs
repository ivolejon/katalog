using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Katalog.Api.Api.Filters;

/// <summary>
/// Validates a request argument with its FluentValidation validator (resolved from DI per
/// request) and returns a 400 ValidationProblem response on failure (arch report §2.5).
/// </summary>
public sealed class ValidationFilter<T>() : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var arg = context.Arguments.OfType<T>().FirstOrDefault();
        if (arg is null)
            return await next(context);

        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is null)
            return await next(context);

        var validationResult = await validator.ValidateAsync(arg);
        if (validationResult.IsValid)
            return await next(context);

        return TypedResults.ValidationProblem(validationResult.ToDictionary());
    }
}
