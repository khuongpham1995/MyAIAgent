/**
 * MyAIAgent | Common · Application
 *
 * Runs FluentValidation before a MediatR stream begins so invalid input never reaches the stream handler.
 */

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FluentValidation;
using MediatR;

namespace MyAIAgent.Common.Application.Behaviors;

// IPipelineBehavior does not run for IStreamRequest, so streams need their own validation hook.
public sealed class StreamValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public async IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var validationResults = await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(context, cancellationToken)));
            var failures = validationResults
                .SelectMany(r => r.Errors)
                .Where(f => f is not null)
                .ToImmutableArray();

            if (failures.Length > 0)
                throw new ValidationException(failures);
        }

        await foreach (var item in next().WithCancellation(cancellationToken))
            yield return item;
    }
}
