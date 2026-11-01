/**
 * MyAIAgent | Common · Infrastructure
 *
 * Carries a bounded destination-policy reason without exposing URLs or resolved addresses.
 */

namespace MyAIAgent.Common.Infrastructure.Http;

public sealed class OutboundDestinationBlockedException(string reason)
    : HttpRequestException("Outbound destination is not allowed.", null, System.Net.HttpStatusCode.Forbidden)
{
    public string Reason { get; } = reason;

    public static OutboundDestinationBlockedException? Find(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is OutboundDestinationBlockedException blocked)
                return blocked;
            if (exception is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                    if (Find(inner) is { } nested)
                        return nested;
            }
            exception = exception.InnerException;
        }
        return null;
    }
}
