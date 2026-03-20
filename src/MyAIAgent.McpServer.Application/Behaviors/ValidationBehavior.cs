using System.Collections.Immutable;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using MyAIAgent.McpServer.Domain.Models;

namespace MyAIAgent.McpServer.Application.Behaviors;

public class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : class
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToImmutableArray();

        if (failures.Length == 0)
        {
            return await next(cancellationToken);
        }

        if (!typeof(TResponse).IsGenericType ||
            typeof(TResponse).GetGenericTypeDefinition() != typeof(ResponseModel<>))
        {
            throw new ValidationException(
                failures.Select(f =>
                    new ValidationFailure(f.PropertyName, f.ErrorMessage)));
        }
        
        var failMethod = typeof(ResponseModel<>)
            .MakeGenericType(typeof(TResponse).GenericTypeArguments[0])
            .GetMethod(nameof(ResponseModel<object>.Fail));
        
        var errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => string.Join(" | ", g.Select(f => f.ErrorMessage)));
        
        return (TResponse)failMethod!.Invoke(
            null,
            [errors, "Validation failed"])!;
    }
}