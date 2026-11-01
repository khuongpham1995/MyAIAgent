/**
 * MyAIAgent | Common · Infrastructure
 *
 * Adapts asynchronous events to SSE while exposing failures before the first event as ordinary HTTP errors.
 */

using Microsoft.AspNetCore.Http;
using System.Net.ServerSentEvents;

namespace MyAIAgent.Common.Infrastructure.Extensions;

public static class ServerSentEventsExtension
{
    /// <summary>
    /// Pulls the first item before returning the result, so failures raised while a stream starts
    /// (validation, lookup) still reach the exception handler as a normal error response.
    /// </summary>
    public static async Task<IResult> ToServerSentEventsAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, string> eventType,
        CancellationToken cancellationToken)
    {
        var enumerator = source.GetAsyncEnumerator(cancellationToken);

        T? first = default;
        bool hasFirst;
        try
        {
            hasFirst = await enumerator.MoveNextAsync();
            if (hasFirst)
                first = enumerator.Current;
        }
        catch
        {
            await enumerator.DisposeAsync();
            throw;
        }

        return TypedResults.ServerSentEvents(Replay(first, hasFirst, enumerator, eventType));
    }

    private static async IAsyncEnumerable<SseItem<T>> Replay<T>(
        T? first,
        bool hasFirst,
        IAsyncEnumerator<T> enumerator,
        Func<T, string> eventType)
    {
        await using (enumerator)
        {
            if (!hasFirst)
                yield break;

            yield return new SseItem<T>(first!, eventType(first!));

            while (await enumerator.MoveNextAsync())
                yield return new SseItem<T>(enumerator.Current, eventType(enumerator.Current));
        }
    }
}
