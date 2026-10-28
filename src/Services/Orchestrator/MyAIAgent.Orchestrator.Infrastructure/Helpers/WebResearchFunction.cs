/**
 * MyAIAgent | Orchestrator · Infrastructure
 *
 * Bounds first-party web invocations and records their successful evidence without changing function declarations.
 */

using System.Text.Json;
using Microsoft.Extensions.AI;

namespace MyAIAgent.Orchestrator.Infrastructure.Helpers;

public sealed class WebResearchFunction(AIFunction innerFunction, WebResearchSources sources) : DelegatingAIFunction(innerFunction)
{
    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!sources.TryBeginInvocation())
            return JsonSerializer.SerializeToElement(new
            {
                isError = true,
                content = new[] { new { type = "text", text = "Web research tool limit reached for this turn. Answer using the available evidence and explain any uncertainty." } }
            });

        var result = await InnerFunction.InvokeAsync(arguments, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        sources.Record(Name, result);
        return result;
    }
}
